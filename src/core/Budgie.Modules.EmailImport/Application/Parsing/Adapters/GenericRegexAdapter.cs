using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Budgie.Modules.EmailImport.Application.Parsing.Normalization;
using Budgie.Modules.EmailImport.Domain.Parsing;
using Budgie.Modules.EmailImport.Domain.Rules;
using Budgie.Shared.Enums;
// EmailImportRule (Domain.Rules) travels on ParseOutcome.MatchedRule so the sync service
// can turn this extraction into a CreateTransactionCommand (slice 11).

namespace Budgie.Modules.EmailImport.Application.Parsing.Adapters;

/// <summary>
/// The only bank adapter in v1: extracts transaction fields from the plain text of an email
/// using the user-configurable regex patterns stored in <c>EmailImportRule.FieldPatternsJson</c>
/// (keys: <c>amount</c>, <c>date</c>, <c>merchant</c>, <c>direction</c>, <c>symbol</c>; the
/// domain guarantees they compile before a rule can be persisted).
/// </summary>
/// <remarks>
/// Scoring follows spec §4.5 exactly: each field gets 0..1 with a concrete reason, and the
/// total is <see cref="ConfidenceScorer.Compute"/> over <see cref="ConfidenceWeights"/>. The
/// compiled regexes are cached per <c>RuleId</c>: compiling per message is the difference
/// between ~1 ms and ~40 ms once a sync batch carries tens of messages. Stateless apart from
/// that cache, so it is registered as a singleton.
/// </remarks>
internal sealed class GenericRegexAdapter : IBankAdapter
{
    /// <summary>Capturing group names inside the field patterns (as in the spec's example JSON).</summary>
    private const string AmountGroup = "amount";
    private const string DateGroup = "date";
    private const string MerchantGroup = "merchant";
    private const string SymbolGroup = "symbol";

    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(2);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Bank wording that carries no payee information ("Transferencia", "Bizum", …). Compared
    /// case-insensitively and accent-free so "Operación" and "operacion" hit the same entry.
    /// </summary>
    private static readonly HashSet<string> BoilerplateMerchants = new(StringComparer.Ordinal)
    {
        "transferencia", "payment", "bizum", "pago", "operacion", "sepa",
        "recibo", "direct debit", "card payment"
    };

    /// <summary>
    /// Expense-side direction keywords. A <c>direction</c> pattern matching one of these means
    /// the pattern is ambiguous (it fires on both credits and debits), so the match cannot
    /// decide the type: the rule's type is kept and the score drops to 0.
    /// </summary>
    private static readonly HashSet<string> ExpenseDirectionKeywords = new(StringComparer.Ordinal)
    {
        "cargo", "cargos", "debito", "debit", "payment", "payments", "pago", "pagos"
    };

    /// <summary>
    /// Cache key: the rule id AND the hash of its patterns.
    /// <para>
    /// Keying on <c>RuleId</c> alone was a real bug: <c>UpdateRule</c> revalidates and persists
    /// the SAME id with new patterns, so a user who fixed a broken regex kept getting the old
    /// compiled patterns — the sync silently kept mis-parsing until the process restarted.
    /// Hashing the patterns makes an edit a cache miss. Draft previews (a fresh Guid per
    /// preview) also land on distinct keys, which is why the cache is capped.
    /// </para>
    /// </summary>
    private readonly ConcurrentDictionary<(Guid RuleId, int PatternsHash), CompiledRuleSet> _compiledRules = new();

    /// <summary>Cap so repeated draft previews cannot grow the cache without bound.</summary>
    private const int MaxCacheEntries = 256;

    private int _compilationCount;

    public string Key => EmailImportRule.GenericRegexKey;

    /// <summary>
    /// Number of regex compilations performed by this instance. Exposed so tests can assert the
    /// per-<c>RuleId</c> cache actually works: parsing the same rule twice must compile once.
    /// </summary>
    public int CompilationCount => Volatile.Read(ref _compilationCount);

    public ParseOutcome Parse(EmailParseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var compiled = GetOrCompile(context.Rule);
        var plainText = context.PlainText;

        // ── amount (mandatory) ──────────────────────────────────────────────────────────
        // No pattern or no match means there is no transaction to extract. An amount is
        // never invented: a wrong auto-committed amount is far worse than a skipped email.
        if (compiled.Amount is null)
            return ParseOutcome.NoMatch("amount-pattern-missing");

        var amountMatches = compiled.Amount.Regex.Matches(plainText);
        if (amountMatches.Count == 0)
            return ParseOutcome.NoMatch("amount-not-found");

        var firstAmount = amountMatches[0];
        var rawAmount = Capture(firstAmount, AmountGroup, compiled.Amount);

        // The stored amount is always positive: the sign decides nothing here (direction comes
        // from the `direction` pattern or from the rule), but an explicit captured '-' is a
        // strong signal used further down to pin the direction score.
        var parsedOk = AmountNormalizer.TryParse(rawAmount, context.LanguageCode, out var parsedAmount);
        var explicitNegative = rawAmount.TrimStart().StartsWith('-') || (parsedOk && parsedAmount < 0);
        var amount = parsedOk ? Math.Abs(parsedAmount) : 0m;

        // Match-count penalty: 2 matches → 0.5, 3+ → 0.3 (and the row is flagged for review).
        // This is what routes "previous balance / amount / new balance" emails to a human.
        var amountScore = !parsedOk
            ? 0.0m // captured text is not a number under this language: trust nothing
            : amountMatches.Count switch
            {
                1 => 1.0m,
                2 => 0.5m,
                _ => 0.3m
            };
        var multipleCandidates = amountMatches.Count >= 2;

        // ── direction ───────────────────────────────────────────────────────────────────
        var transactionType = context.Rule.TransactionType;
        var directionScore = 1.0m; // the rule fixes the type explicitly: that alone is a 1.0
        if (compiled.Direction is not null)
        {
            var directionMatch = compiled.Direction.Regex.Match(plainText);
            if (directionMatch.Success)
            {
                if (IsExpenseKeyword(directionMatch.Value))
                {
                    // Ambiguous in both senses: the pattern fired on an expense-side keyword,
                    // so a match no longer proves Income. Keep the rule's type, trust nothing.
                    directionScore = 0.0m;
                }
                else
                {
                    // Spec §4.4: a direction match means Income; the user writes the pattern
                    // to match credit keywords (abono, ingreso, …).
                    transactionType = TransactionType.Income;
                }
            }
        }

        // An explicitly captured '-' (e.g. "-45,20 €") is a strong outflow signal: it pins the
        // direction score to 1.0 even when the pattern itself said little.
        if (explicitNegative)
            directionScore = 1.0m;

        // ── merchant ────────────────────────────────────────────────────────────────────
        // First match wins. No pattern or empty capture scores 0: nothing captured is nothing
        // captured — a merchant is never guessed from the subject or the sender.
        string? merchant = null;
        var merchantScore = 0.0m;
        if (compiled.Merchant is not null)
        {
            var merchantMatch = compiled.Merchant.Regex.Match(plainText);
            if (merchantMatch.Success)
            {
                merchant = Capture(merchantMatch, MerchantGroup, compiled.Merchant).Trim();
                if (merchant.Length == 0)
                {
                    merchant = null; // whitespace-only capture: not a merchant
                }
                else
                {
                    // 0.3 for boilerplate: the text exists but names no payee ("Transferencia"
                    // describes the operation type, not who it went to), so the review row
                    // still gets something to show without pretending it is a merchant.
                    merchantScore = IsBoilerplate(merchant) ? 0.3m : 1.0m;
                }
            }
        }

        // ── date ────────────────────────────────────────────────────────────────────────
        DateTime date;
        var dateScore = 0.0m;
        if (compiled.Date is null)
        {
            // No date pattern: the receipt timestamp is reliable but carries the time the
            // email arrived, not the day of the operation — hence 0.5, not 1.0.
            date = context.Message.ReceivedAt.Date;
            dateScore = 0.5m;
        }
        else
        {
            var distinctDates = new HashSet<DateTime>();
            DateTime? parsedDate = null;
            foreach (Match dateMatch in compiled.Date.Regex.Matches(plainText))
            {
                var rawDate = Capture(dateMatch, DateGroup, compiled.Date);
                if (DateNormalizer.TryParse(rawDate, context.LanguageCode, out var candidate))
                {
                    distinctDates.Add(candidate);
                    parsedDate ??= candidate; // first match wins for the stored value
                }
            }

            if (parsedDate is null)
            {
                // Unparseable: keep the receipt date as the stored value so the review row
                // has something to show, but with score 0 — the date cannot be trusted.
                date = context.Message.ReceivedAt.Date;
                dateScore = 0.0m;
            }
            else
            {
                date = parsedDate.Value;
                dateScore = date > context.Message.ReceivedAt.Date
                    ? 0.0m // a future operation date is almost always a misread (dd/mm swap)
                    : distinctDates.Count >= 2
                        ? 0.5m // several distinct dates: the pattern is not anchored enough
                        : 1.0m;
            }
        }

        // ── currency ────────────────────────────────────────────────────────────────────
        // No captured symbol → the rule declares the currency explicitly: 1.0. A captured
        // symbol must agree with the rule or the row goes to review with 0.
        var currency = context.Rule.Currency;
        var currencyScore = 1.0m;
        var symbol = CaptureSymbol(context, compiled, firstAmount);
        if (symbol is not null)
        {
            var mapped = MapSymbol(symbol);
            currencyScore = mapped is not null &&
                            string.Equals(mapped, currency, StringComparison.OrdinalIgnoreCase)
                ? 1.0m
                : 0.0m; // unmappable or contradicting symbol: do not trust the currency
        }

        var fieldScores = new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            [ConfidenceScorer.AmountField] = amountScore,
            [ConfidenceScorer.DirectionField] = directionScore,
            [ConfidenceScorer.MerchantField] = merchantScore,
            [ConfidenceScorer.DateField] = dateScore,
            [ConfidenceScorer.CurrencyField] = currencyScore
        };

        return ParseOutcome.Success(context.Rule, new ExtractedTransaction(
            Amount: amount,
            Currency: currency,
            Date: date,
            Merchant: merchant,
            TransactionType: transactionType,
            Confidence: ConfidenceScorer.Compute(fieldScores),
            FieldConfidence: fieldScores,
            MultipleAmountCandidates: multipleCandidates));
    }

    /// <summary>
    /// Returns the compiled rule set for <paramref name="rule"/>, compiling its patterns on
    /// first use. The factory may run twice under a first-access race (ConcurrentDictionary
    /// semantics) and then <see cref="CompilationCount"/> briefly over-counts; the cache stays
    /// correct and the counter is a diagnostic, not a ledger.
    /// </summary>
    private CompiledRuleSet GetOrCompile(EmailImportRule rule)
    {
        var key = (rule.Id, ComputePatternsHash(rule));

        if (_compiledRules.Count >= MaxCacheEntries)
            _compiledRules.Clear();

        return _compiledRules.GetOrAdd(key, _ =>
        {
            var set = Compile(rule);
            Interlocked.Increment(ref _compilationCount);
            return set;
        });
    }

    /// <summary>
    /// Stable hash of the patterns that affect compilation. Only the fields <c>Compile</c>
    /// actually reads are included, so a change to, say, the rule's name is not a cache miss.
    /// </summary>
    private static int ComputePatternsHash(EmailImportRule rule)
    {
        var patterns = rule.FieldPatternsJson;
        if (string.IsNullOrWhiteSpace(patterns))
            return 0;

        return StringComparer.Ordinal.GetHashCode(patterns);
    }

    private static CompiledRuleSet Compile(EmailImportRule rule)
    {
        // The domain already validated that these regexes compile before the rule could be
        // persisted; a bad JSON here is therefore corruption, not user input — swallow it and
        // let the amount-pattern-missing branch skip the email instead of failing the sync.
        FieldPatternSet? patterns = null;
        if (!string.IsNullOrWhiteSpace(rule.FieldPatternsJson))
        {
            try
            {
                patterns = JsonSerializer.Deserialize<FieldPatternSet>(rule.FieldPatternsJson, JsonOptions);
            }
            catch (JsonException)
            {
                patterns = null;
            }
        }

        return new CompiledRuleSet(
            ToCompiled(patterns?.Amount),
            ToCompiled(patterns?.Date),
            ToCompiled(patterns?.Merchant),
            ToCompiled(patterns?.Direction),
            ToCompiled(patterns?.Symbol));
    }

    private static CompiledPattern? ToCompiled(string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return null;

        // CultureInvariant on top of the spec's IgnoreCase|Multiline: the adapter is a
        // singleton shared by every request, and culture-sensitive casing (tr-TR "I") must
        // not change matching results depending on which thread serves the user.
        var regex = new Regex(
            pattern!,
            RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant,
            RegexTimeout);

        // Group names are precomputed so per-message parsing never re-enumerates them.
        return new CompiledPattern(regex, new HashSet<string>(regex.GetGroupNames(), StringComparer.Ordinal));
    }

    /// <summary>
    /// Returns the text of the named capture group when the pattern defines it and it
    /// participated in the match; otherwise the whole match (patterns without named groups
    /// are supported: the full match is the captured value).
    /// </summary>
    private static string Capture(Match match, string group, CompiledPattern pattern) =>
        pattern.Groups.Contains(group) && match.Groups[group].Success
            ? match.Groups[group].Value
            : match.Value;

    /// <summary>
    /// Resolves the currency symbol: the <c>symbol</c> group inside the amount pattern first
    /// (the spec's example embeds it there, anchored to the amount), then the standalone
    /// <c>symbol</c> pattern. Null when nothing was captured.
    /// </summary>
    private static string? CaptureSymbol(EmailParseContext context, CompiledRuleSet compiled, Match firstAmount)
    {
        if (compiled.Amount is not null && compiled.Amount.Groups.Contains(SymbolGroup))
        {
            var fromAmount = firstAmount.Groups[SymbolGroup].Value.Trim();
            if (fromAmount.Length > 0)
                return fromAmount;
        }

        if (compiled.Symbol is not null)
        {
            var match = compiled.Symbol.Regex.Match(context.PlainText);
            if (match.Success)
            {
                var value = Capture(match, SymbolGroup, compiled.Symbol).Trim();
                if (value.Length > 0)
                    return value;
            }
        }

        return null;
    }

    /// <summary>Maps a captured symbol to an ISO 4217 code; null when the symbol is unknown.</summary>
    private static string? MapSymbol(string symbol) =>
        symbol.Contains('€') || symbol.Contains("eur", StringComparison.OrdinalIgnoreCase) ? "EUR" :
        symbol.Contains('$') || symbol.Contains("usd", StringComparison.OrdinalIgnoreCase) ? "USD" :
        symbol.Contains('£') || symbol.Contains("gbp", StringComparison.OrdinalIgnoreCase) ? "GBP" :
        null;

    /// <summary>True when the matched direction text is an expense-side keyword (ambiguous pattern).</summary>
    private static bool IsExpenseKeyword(string matched) =>
        ExpenseDirectionKeywords.Contains(Normalize(matched));

    /// <summary>True when the merchant is bank boilerplate rather than a payee name.</summary>
    private static bool IsBoilerplate(string merchant) =>
        BoilerplateMerchants.Contains(Normalize(merchant));

    /// <summary>Lowercases and strips accents so "Operación" and "operacion" compare equal.</summary>
    private static string Normalize(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value.ToLowerInvariant())
        {
            sb.Append(ch switch
            {
                'á' or 'à' or 'ä' => 'a',
                'é' or 'è' or 'ë' => 'e',
                'í' or 'ì' or 'ï' => 'i',
                'ó' or 'ò' or 'ö' => 'o',
                'ú' or 'ù' or 'ü' => 'u',
                'ñ' => 'n',
                _ => ch
            });
        }
        return sb.ToString();
    }

    /// <summary>
    /// JSON shape of <c>EmailImportRule.FieldPatternsJson</c>. The keys are exactly the ones
    /// the domain validates (<c>amount</c>, <c>date</c>, <c>merchant</c>, <c>direction</c>,
    /// <c>symbol</c>); <c>JsonSerializerDefaults.Web</c> binds them case-insensitively.
    /// Private because nothing outside this adapter needs the shape.
    /// </summary>
    private sealed record FieldPatternSet(
        string? Amount,
        string? Date,
        string? Merchant,
        string? Direction,
        string? Symbol);

    /// <summary>A compiled field pattern plus its named capture groups (precomputed).</summary>
    private sealed record CompiledPattern(Regex Regex, HashSet<string> Groups);

    /// <summary>Compiled patterns of one rule, cached per RuleId.</summary>
    private sealed record CompiledRuleSet(
        CompiledPattern? Amount,
        CompiledPattern? Date,
        CompiledPattern? Merchant,
        CompiledPattern? Direction,
        CompiledPattern? Symbol);
}
