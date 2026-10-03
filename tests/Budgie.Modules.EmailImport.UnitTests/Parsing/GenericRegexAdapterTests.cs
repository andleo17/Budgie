using System.Text.Json;
using Budgie.Modules.EmailImport.Application.Parsing;
using Budgie.Modules.EmailImport.Application.Parsing.Adapters;
using Budgie.Modules.EmailImport.Domain.Parsing;
using Budgie.Modules.EmailImport.Domain.Rules;
using Budgie.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace Budgie.Modules.EmailImport.UnitTests.Parsing;

/// <summary>
/// <see cref="GenericRegexAdapter"/> turns a user's regexes plus the email text into an
/// <see cref="ExtractedTransaction"/>. Two things are asserted here above all: an amount is
/// NEVER invented (no amount pattern or no match means no transaction at all), and the
/// match-count penalty routes ambiguous "previous balance / amount / new balance" emails to
/// a human instead of silently auto-committing the wrong number.
/// </summary>
public class GenericRegexAdapterTests
{
    private static readonly Guid AccountId = Guid.NewGuid();

    private static EmailImportRule Rule(
        string? amount = null,
        string? date = null,
        string? merchant = null,
        string? direction = null,
        string? symbol = null,
        TransactionType type = TransactionType.Expense,
        bool autoCommit = false) =>
        EmailImportRule.Create(
            userId: Guid.NewGuid(),
            name: "Regla de prueba",
            adapterKey: EmailImportRule.GenericRegexKey,
            priority: 0,
            isEnabled: true,
            accountId: AccountId,
            categoryId: null,
            transactionType: type,
            currency: "EUR",
            exchangeRate: null,
            fromAddress: null,
            subjectContains: null,
            bodyRegex: null,
            fieldPatternsJson: Patterns(amount, date, merchant, direction, symbol),
            autoCommit: autoCommit);

    private static string Patterns(
        string? amount, string? date, string? merchant, string? direction, string? symbol)
    {
        var map = new Dictionary<string, string?>();
        if (amount is not null) map["amount"] = amount;
        if (date is not null) map["date"] = date;
        if (merchant is not null) map["merchant"] = merchant;
        if (direction is not null) map["direction"] = direction;
        if (symbol is not null) map["symbol"] = symbol;
        return JsonSerializer.Serialize(map);
    }

    private static EmailParseContext Context(
        string plainText, EmailImportRule rule, string language = "es",
        DateTimeOffset? receivedAt = null) =>
        new(
            EmailMessage.Create("m1", "t1", "banco@ejemplo.es", "Aviso", "raw",
                receivedAt ?? new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero)),
            plainText,
            rule,
            language);

    private const string SpanishBankEmail =
        "Banco Ejemplo\n" +
        "Fecha: 01/10/2026\n" +
        "Importe: 45,20 €\n" +
        "Comercio: Cafetería Central\n";

    // ── The happy path ─────────────────────────────────────────────────────────────────
    [Fact]
    public void Parse_AllFieldsCaptured_ExtractsEveryValue()
    {
        var rule = Rule(
            amount: @"Importe:\s*(?<amount>[\d.,]+)",
            date: @"Fecha:\s*(?<date>\d{2}/\d{2}/\d{4})",
            merchant: @"Comercio:\s*(?<merchant>.+)");

        var outcome = new GenericRegexAdapter().Parse(Context(SpanishBankEmail, rule));

        outcome.Matched.Should().BeTrue();
        outcome.SkipReason.Should().BeNull();
        outcome.Value!.Amount.Should().Be(45.20m);
        outcome.Value.Currency.Should().Be("EUR");
        outcome.Value.Date.Should().Be(new DateTime(2026, 10, 1));
        outcome.Value.Merchant.Should().Be("Cafetería Central");
        outcome.Value.TransactionType.Should().Be(TransactionType.Expense);
    }

    [Fact]
    public void Parse_SuccessfulOutcome_CarriesTheRuleAsProvenance()
    {
        var rule = Rule(amount: @"Importe:\s*(?<amount>[\d.,]+)");

        var outcome = new GenericRegexAdapter().Parse(Context(SpanishBankEmail, rule));

        outcome.MatchedRule.Should().BeSameAs(rule);
    }

    [Fact]
    public void Parse_PatternWithoutNamedGroups_UsesTheWholeMatch()
    {
        var rule = Rule(amount: @"[\d]+,[0-9]{2}");

        var outcome = new GenericRegexAdapter().Parse(Context(SpanishBankEmail, rule));

        outcome.Matched.Should().BeTrue();
        outcome.Value!.Amount.Should().Be(45.20m);
    }

    // ── An amount is never invented ────────────────────────────────────────────────────
    [Fact]
    public void Parse_NoAmountPattern_ReturnsNoMatch()
    {
        var rule = Rule(merchant: @"Comercio:\s*(?<merchant>.+)");

        var outcome = new GenericRegexAdapter().Parse(Context(SpanishBankEmail, rule));

        outcome.Matched.Should().BeFalse();
        outcome.Value.Should().BeNull();
        outcome.SkipReason.Should().Be("amount-pattern-missing");
    }

    [Fact]
    public void Parse_AmountPatternDoesNotMatch_ReturnsNoMatch()
    {
        var rule = Rule(amount: @"Importe:\s*(?<amount>[\d.,]+)");

        var outcome = new GenericRegexAdapter().Parse(Context("Un correo sin importe alguno.", rule));

        outcome.Matched.Should().BeFalse();
        outcome.SkipReason.Should().Be("amount-not-found");
    }

    [Fact]
    public void Parse_EmptyPatternJson_ReturnsNoMatchInsteadOfThrowing()
    {
        // The domain guarantees the patterns compile, so bad JSON here is corruption; the
        // adapter must swallow it and skip the email rather than kill the whole sync.
        var rule = Rule(amount: null);
        typeof(EmailImportRule)
            .GetProperty(nameof(EmailImportRule.FieldPatternsJson))!
            .SetValue(rule, "{ this is not json");

        var act = () => new GenericRegexAdapter().Parse(Context(SpanishBankEmail, rule));

        act.Should().NotThrow();
        act().Matched.Should().BeFalse();
        act().SkipReason.Should().Be("amount-pattern-missing");
    }

    [Fact]
    public void Parse_CapturedTextIsNotANumber_ScoresZeroButStillMatches()
    {
        // The pattern matched but the capture is not a number: trust nothing, yet the
        // outcome is still a match so the row reaches the review inbox with amount 0.
        var rule = Rule(amount: @"Aviso:\s*(?<amount>\w+)");

        var outcome = new GenericRegexAdapter().Parse(Context("Aviso: pendiente", rule));

        outcome.Matched.Should().BeTrue();
        outcome.Value!.Amount.Should().Be(0m);
        outcome.Value.FieldConfidence[ConfidenceScorer.AmountField].Should().Be(0m);
    }

    // ── The match-count penalty: the case that routes emails to a human ────────────────
    [Fact]
    public void Parse_SingleAmountMatch_ScoresOne()
    {
        var rule = Rule(amount: @"Importe:\s*(?<amount>[\d.,]+)");

        var outcome = new GenericRegexAdapter().Parse(Context(SpanishBankEmail, rule));

        outcome.Value!.FieldConfidence[ConfidenceScorer.AmountField].Should().Be(1.0m);
        outcome.Value.MultipleAmountCandidates.Should().BeFalse();
    }

    [Fact]
    public void Parse_TwoAmountMatches_ScoresHalfAndFlagsTheRow()
    {
        var text = "Importe: 45,20 €\nComisión: 1,50 €";
        var rule = Rule(amount: @"[0-9]+,[0-9]{2}");

        var outcome = new GenericRegexAdapter().Parse(Context(text, rule));

        outcome.Value!.FieldConfidence[ConfidenceScorer.AmountField].Should().Be(0.5m);
        outcome.Value.MultipleAmountCandidates.Should().BeTrue();
    }

    [Fact]
    public void Parse_ThreeAmountMatches_ScoresThreeTenthsAndFlagsTheRow()
    {
        // The classic "saldo anterior / importe / saldo nuevo" email. Note the ORDER: the
        // real "Importe:" line comes FIRST, because the adapter takes amountMatches[0] and
        // the balance lines would otherwise win — this is the exact trap that makes the
        // "3 matches" case meaningful rather than accidental.
        var text =
            "Importe: 45,20 €\n" +
            "Saldo anterior: 1.000,00 €\n" +
            "Saldo nuevo: 954,80 €";
        var rule = Rule(amount: @"[0-9.]+,[0-9]{2}");

        var outcome = new GenericRegexAdapter().Parse(Context(text, rule));

        outcome.Value!.FieldConfidence[ConfidenceScorer.AmountField].Should().Be(0.3m);
        outcome.Value.MultipleAmountCandidates.Should().BeTrue();
        outcome.Value.Amount.Should().Be(45.20m);
    }

    [Fact]
    public void Parse_AmountLinesBeforeTheRealOne_StageTheWrongAmount()
    {
        // The counterpart of the case above: with the balances first, amountMatches[0] IS
        // the previous balance. Pinned deliberately — the adapter does NOT try to pick the
        // semantically right line, it takes the first match and the 0.3 score exists so a
        // human reviews it. Users must anchor their regex.
        var text =
            "Saldo anterior: 1.000,00 €\n" +
            "Importe: 45,20 €\n" +
            "Saldo nuevo: 954,80 €";
        var rule = Rule(amount: @"[0-9.]+,[0-9]{2}");

        var outcome = new GenericRegexAdapter().Parse(Context(text, rule));

        outcome.Value!.Amount.Should().Be(1000.00m);
        outcome.Value.MultipleAmountCandidates.Should().BeTrue();
        outcome.Value.Confidence.Should().BeLessThan(0.85m);
    }

    // ── Direction ──────────────────────────────────────────────────────────────────────
    [Fact]
    public void Parse_DirectionPatternMatchesCreditKeyword_ForcesIncome()
    {
        var rule = Rule(
            amount: @"Importe:\s*(?<amount>[\d.,]+)",
            direction: "abono",
            type: TransactionType.Expense);

        var outcome = new GenericRegexAdapter().Parse(
            Context("Importe: 45,20 €\nabono recibido", rule));

        outcome.Value!.TransactionType.Should().Be(TransactionType.Income);
        outcome.Value.FieldConfidence[ConfidenceScorer.DirectionField].Should().Be(1.0m);
    }

    [Theory]
    [InlineData("cargo")]
    [InlineData("Cargo")]
    [InlineData("débito")]
    [InlineData("pago")]
    [InlineData("payment")]
    public void Parse_DirectionPatternMatchesExpenseKeyword_ScoresZeroAndKeepsRuleType(
        string keyword)
    {
        // An expense-side keyword makes the pattern ambiguous: it fires on credits too, so
        // the match cannot prove Income and the rule's type is kept.
        var rule = Rule(
            amount: @"Importe:\s*(?<amount>[\d.,]+)",
            direction: keyword,
            type: TransactionType.Expense);

        var outcome = new GenericRegexAdapter().Parse(
            Context($"Importe: 45,20 €\n{keyword} aplicado", rule));

        outcome.Value!.TransactionType.Should().Be(TransactionType.Expense);
        outcome.Value.FieldConfidence[ConfidenceScorer.DirectionField].Should().Be(0.0m);
    }

    [Fact]
    public void Parse_NoDirectionPattern_ScoresOneBecauseTheRuleFixesTheType()
    {
        var rule = Rule(amount: @"Importe:\s*(?<amount>[\d.,]+)", type: TransactionType.Income);

        var outcome = new GenericRegexAdapter().Parse(Context(SpanishBankEmail, rule));

        outcome.Value!.FieldConfidence[ConfidenceScorer.DirectionField].Should().Be(1.0m);
        outcome.Value.TransactionType.Should().Be(TransactionType.Income);
    }

    [Fact]
    public void Parse_ExplicitlyNegativeAmount_PinsTheDirectionScoreToOne()
    {
        // "-45,20 €" is an unambiguous outflow signal even without a direction pattern.
        var rule = Rule(amount: @"Importe:\s*(?<amount>-?[\d.,]+)", type: TransactionType.Income);

        var outcome = new GenericRegexAdapter().Parse(Context("Importe: -45,20 €", rule));

        outcome.Value!.Amount.Should().Be(45.20m);   // stored positive: the sign decides nothing here
        outcome.Value.TransactionType.Should().Be(TransactionType.Income); // rule type still wins
        outcome.Value.FieldConfidence[ConfidenceScorer.DirectionField].Should().Be(1.0m);
    }

    // ── Merchant ───────────────────────────────────────────────────────────────────────
    [Fact]
    public void Parse_ConcreteMerchant_ScoresOne()
    {
        var rule = Rule(
            amount: @"Importe:\s*(?<amount>[\d.,]+)",
            merchant: @"Comercio:\s*(?<merchant>.+)");

        var outcome = new GenericRegexAdapter().Parse(Context(SpanishBankEmail, rule));

        outcome.Value!.FieldConfidence[ConfidenceScorer.MerchantField].Should().Be(1.0m);
    }

    [Theory]
    [InlineData("Transferencia")]
    [InlineData("transferencia")]
    [InlineData("Operación")]
    [InlineData("operacion")]  // accent-insensitive
    [InlineData("Bizum")]
    [InlineData("SEPA")]
    [InlineData("Recibo")]
    [InlineData("Pago")]
    [InlineData("Direct Debit")]
    public void Parse_BoilerplateMerchant_ScoresThreeTenthsButKeepsTheText(string boilerplate)
    {
        // The text exists but names no payee: the review row still gets something to show
        // without pretending "Transferencia" is a merchant.
        var rule = Rule(
            amount: @"Importe:\s*(?<amount>[\d.,]+)",
            merchant: @"Tipo:\s*(?<merchant>.+)");

        var outcome = new GenericRegexAdapter().Parse(
            Context($"Importe: 45,20 €\nTipo: {boilerplate}", rule));

        outcome.Value!.Merchant.Should().Be(boilerplate);
        outcome.Value.FieldConfidence[ConfidenceScorer.MerchantField].Should().Be(0.3m);
    }

    [Fact]
    public void Parse_NoMerchantPattern_ScoresZeroAndLeavesMerchantNull()
    {
        var rule = Rule(amount: @"Importe:\s*(?<amount>[\d.,]+)");

        var outcome = new GenericRegexAdapter().Parse(Context(SpanishBankEmail, rule));

        outcome.Value!.Merchant.Should().BeNull();
        outcome.Value.FieldConfidence[ConfidenceScorer.MerchantField].Should().Be(0m);
    }

    [Fact]
    public void Parse_MerchantPatternDoesNotMatch_ScoresZero()
    {
        var rule = Rule(
            amount: @"Importe:\s*(?<amount>[\d.,]+)",
            merchant: @"Comercio:\s*(?<merchant>.+)");

        var outcome = new GenericRegexAdapter().Parse(Context("Importe: 45,20 €", rule));

        outcome.Value!.Merchant.Should().BeNull();
        outcome.Value.FieldConfidence[ConfidenceScorer.MerchantField].Should().Be(0m);
    }

    [Fact]
    public void Parse_WhitespaceOnlyMerchantCapture_IsTreatedAsNoMerchant()
    {
        var rule = Rule(
            amount: @"Importe:\s*(?<amount>[\d.,]+)",
            merchant: @"Comercio:\s*(?<merchant>[ \t]*)");

        var outcome = new GenericRegexAdapter().Parse(Context(SpanishBankEmail, rule));

        outcome.Value!.Merchant.Should().BeNull();
        outcome.Value.FieldConfidence[ConfidenceScorer.MerchantField].Should().Be(0m);
    }

    // ── Date ───────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Parse_SingleDistinctDateInThePast_ScoresOne()
    {
        var rule = Rule(
            amount: @"Importe:\s*(?<amount>[\d.,]+)",
            date: @"Fecha:\s*(?<date>\d{2}/\d{2}/\d{4})");

        var outcome = new GenericRegexAdapter().Parse(Context(SpanishBankEmail, rule));

        outcome.Value!.FieldConfidence[ConfidenceScorer.DateField].Should().Be(1.0m);
    }

    [Fact]
    public void Parse_SeveralDistinctDates_ScoresHalf()
    {
        // A pattern loose enough to hit two different dates proves nothing about which one
        // is the operation date.
        var text = "Fecha de operación: 01/10/2026\nFecha de valor: 02/10/2026\nImporte: 45,20 €";
        var rule = Rule(
            amount: @"Importe:\s*(?<amount>[\d.,]+)",
            date: @"\d{2}/\d{2}/\d{4}");

        var outcome = new GenericRegexAdapter().Parse(Context(text, rule));

        outcome.Value!.FieldConfidence[ConfidenceScorer.DateField].Should().Be(0.5m);
        outcome.Value.Date.Should().Be(new DateTime(2026, 10, 1)); // first match wins
    }

    [Fact]
    public void Parse_RepeatedIdenticalDate_ScoresOneNotHalf()
    {
        // Two matches of the SAME date are not ambiguity: only DISTINCT dates count.
        var text = "Fecha: 01/10/2026\nFechaLiquidación: 01/10/2026\nImporte: 45,20 €";
        var rule = Rule(
            amount: @"Importe:\s*(?<amount>[\d.,]+)",
            date: @"\d{2}/\d{2}/\d{4}");

        new GenericRegexAdapter().Parse(Context(text, rule))
            .Value!.FieldConfidence[ConfidenceScorer.DateField].Should().Be(1.0m);
    }

    [Fact]
    public void Parse_FutureOperationDate_ScoresZero()
    {
        // A date after the receipt is almost always a misread dd/mm swap.
        var text = "Importe: 45,20 €\nFecha: 20/10/2026";
        var rule = Rule(
            amount: @"Importe:\s*(?<amount>[\d.,]+)",
            date: @"Fecha:\s*(?<date>\d{2}/\d{2}/\d{4})");

        var outcome = new GenericRegexAdapter().Parse(Context(text, rule));

        outcome.Value!.FieldConfidence[ConfidenceScorer.DateField].Should().Be(0.0m);
        outcome.Value.Date.Should().Be(new DateTime(2026, 10, 20));
    }

    [Fact]
    public void Parse_UnparseableDate_FallsBackToTheReceiptDateWithScoreZero()
    {
        var text = "Importe: 45,20 €\nFecha:eday";
        var rule = Rule(
            amount: @"Importe:\s*(?<amount>[\d.,]+)",
            date: @"Fecha:\s*(?<date>\w+)");

        var receivedAt = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
        var outcome = new GenericRegexAdapter().Parse(Context(text, rule, receivedAt: receivedAt));

        outcome.Value!.Date.Should().Be(new DateTime(2026, 10, 2));
        outcome.Value.FieldConfidence[ConfidenceScorer.DateField].Should().Be(0.0m);
    }

    [Fact]
    public void Parse_NoDatePattern_UsesTheReceiptDateWithScoreHalf()
    {
        // The receipt timestamp is reliable but carries the arrival time, not the day of
        // the operation — hence 0.5, not 1.0.
        var rule = Rule(amount: @"Importe:\s*(?<amount>[\d.,]+)");
        var receivedAt = new DateTimeOffset(2026, 10, 2, 12, 30, 0, TimeSpan.Zero);

        var outcome = new GenericRegexAdapter().Parse(Context(SpanishBankEmail, rule, receivedAt: receivedAt));

        outcome.Value!.Date.Should().Be(new DateTime(2026, 10, 2));
        outcome.Value.FieldConfidence[ConfidenceScorer.DateField].Should().Be(0.5m);
    }

    [Fact]
    public void Parse_DateUsesTheRuleLanguage()
    {
        var rule = Rule(
            amount: @"Amount:\s*(?<amount>[\d.,]+)",
            date: @"Date:\s*(?<date>[\d/]+)");

        var spanish = new GenericRegexAdapter().Parse(
            Context("Date: 01/02/2026\nAmount: 45,20", rule, "es"));
        var english = new GenericRegexAdapter().Parse(
            Context("Date: 01/02/2026\nAmount: 45.20", rule, "en"));

        spanish.Value!.Date.Should().Be(new DateTime(2026, 2, 1));
        english.Value!.Date.Should().Be(new DateTime(2026, 1, 2));
    }

    // ── Currency ───────────────────────────────────────────────────────────────────────
    [Fact]
    public void Parse_NoCapturedSymbol_ScoresOneBecauseTheRuleDeclaresIt()
    {
        var rule = Rule(amount: @"Importe:\s*(?<amount>[\d.,]+)");

        new GenericRegexAdapter().Parse(Context(SpanishBankEmail, rule))
            .Value!.FieldConfidence[ConfidenceScorer.CurrencyField].Should().Be(1.0m);
    }

    [Fact]
    public void Parse_CapturedSymbolInsideTheAmountGroup_AgreesWithTheRule()
    {
        var rule = Rule(amount: @"Importe:\s*(?<symbol>€)?\s*(?<amount>[\d.,]+)");

        var outcome = new GenericRegexAdapter().Parse(Context(SpanishBankEmail, rule));

        outcome.Value!.Currency.Should().Be("EUR");
        outcome.Value.FieldConfidence[ConfidenceScorer.CurrencyField].Should().Be(1.0m);
    }

    [Fact]
    public void Parse_StandaloneSymbolPattern_AgreesWithTheRule()
    {
        var rule = Rule(
            amount: @"Importe:\s*(?<amount>[\d.,]+)",
            symbol: @"(?<symbol>€)");

        new GenericRegexAdapter().Parse(Context(SpanishBankEmail, rule))
            .Value!.FieldConfidence[ConfidenceScorer.CurrencyField].Should().Be(1.0m);
    }

    [Fact]
    public void Parse_CapturedSymbolContradictingTheRule_ScoresZero()
    {
        // The message says dollars while the rule declares euros: do not trust the currency.
        var rule = Rule(amount: @"Importe:\s*(?<symbol>\$)?\s*(?<amount>[\d.,]+)");

        var outcome = new GenericRegexAdapter().Parse(Context("Importe: $45,20", rule));

        outcome.Value!.Currency.Should().Be("EUR"); // the rule's value is what is stored
        outcome.Value.FieldConfidence[ConfidenceScorer.CurrencyField].Should().Be(0.0m);
    }

    [Fact]
    public void Parse_UnknownSymbol_ScoresZero()
    {
        var rule = Rule(
            amount: @"Importe:\s*(?<amount>[\d.,]+)",
            symbol: @"(?<symbol>¥)");

        new GenericRegexAdapter().Parse(Context("Importe: 45,20 ¥", rule))
            .Value!.FieldConfidence[ConfidenceScorer.CurrencyField].Should().Be(0.0m);
    }

    // ── The per-RuleId compilation cache ───────────────────────────────────────────────
    [Fact]
    public void Parse_SameRuleTwice_CompilesThePatternsOnlyOnce()
    {
        var adapter = new GenericRegexAdapter();
        var rule = Rule(amount: @"Importe:\s*(?<amount>[\d.,]+)");

        adapter.Parse(Context(SpanishBankEmail, rule));
        var afterFirst = adapter.CompilationCount;
        adapter.Parse(Context(SpanishBankEmail, rule));

        afterFirst.Should().Be(1);
        adapter.CompilationCount.Should().Be(1);
    }

    [Fact]
    public void Parse_DifferentRules_AreCompiledSeparately()
    {
        var adapter = new GenericRegexAdapter();

        adapter.Parse(Context(SpanishBankEmail, Rule(amount: @"Importe:\s*(?<amount>[\d.,]+)")));
        adapter.Parse(Context(SpanishBankEmail, Rule(amount: @"[0-9]+,[0-9]{2}")));

        adapter.CompilationCount.Should().Be(2);
    }

    [Fact]
    public void Parse_SameRuleWithDifferentText_ReusesTheCompiledPatterns()
    {
        var adapter = new GenericRegexAdapter();
        var rule = Rule(amount: @"[\d]+,[0-9]{2}");

        adapter.Parse(Context("Importe: 45,20", rule));
        adapter.Parse(Context("Importe: 99,99", rule));
        adapter.Parse(Context("Importe: 12,00", rule));

        adapter.CompilationCount.Should().Be(1);
    }

    [Fact]
    public void Key_IsGenericRegex()
    {
        new GenericRegexAdapter().Key.Should().Be(EmailImportRule.GenericRegexKey);
    }
}
