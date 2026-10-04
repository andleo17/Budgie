using System.Text.Json;
using System.Text.RegularExpressions;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Domain;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Enums;

namespace Budgie.Modules.EmailImport.Domain.Rules;

/// <summary>
/// User-defined rule that maps incoming emails to a Budgie account.
/// Rules are ordered by <see cref="Priority"/> ascending and the first one whose filters
/// match an email wins. v1 only supports the <see cref="GenericRegexKey"/> adapter.
/// </summary>
/// <remarks>
/// Domain validation is intentional and strict (including regex compilation): an invalid rule
/// must never be persisted, because it would fail silently at sync time instead of at write time.
/// </remarks>
public class EmailImportRule : Entity
{
    /// <summary>Adapter key of the only adapter shipped in v1: user-configurable regexes.</summary>
    public const string GenericRegexKey = "generic.regex";

    /// <summary>Maximum length accepted for <see cref="Name"/> (matches the DDL column).</summary>
    private const int NameMaxLength = 100;

    /// <summary>Maximum length accepted for <see cref="AdapterKey"/> (matches the DDL column).</summary>
    private const int AdapterKeyMaxLength = 50;

    /// <summary>Maximum length accepted for <see cref="FromAddress"/> (matches the DDL column).</summary>
    private const int FromAddressMaxLength = 255;

    /// <summary>Maximum length accepted for <see cref="SubjectContains"/> (matches the DDL column).</summary>
    private const int SubjectContainsMaxLength = 200;

    /// <summary>Maximum length accepted for <see cref="BodyRegex"/> (matches the DDL column).</summary>
    private const int BodyRegexMaxLength = 500;

    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(2);

    private Regex? _bodyRegex;

    public Guid Id { get; internal set; }
    public Guid UserId { get; internal set; }
    public string Name { get; internal set; } = null!;
    public string AdapterKey { get; internal set; } = null!;
    public int Priority { get; internal set; }
    public bool IsEnabled { get; internal set; }
    public Guid AccountId { get; internal set; }
    public Guid? CategoryId { get; internal set; }
    public TransactionType TransactionType { get; internal set; }

    /// <summary>
    /// Canonical currency code (ISO 4217) this rule stamps on parsed transactions.
    /// A value object, not a raw string: the domain validates and normalizes through
    /// <see cref="Budgie.Shared.Domain.ValueObjects.CurrencyCode"/> at write time.
    /// </summary>
    public CurrencyCode Currency { get; internal set; } = null!;
    public decimal? ExchangeRate { get; internal set; }
    public string? FromAddress { get; internal set; }
    public string? SubjectContains { get; internal set; }
    public string? BodyRegex { get; internal set; }
    public string? FieldPatternsJson { get; internal set; }
    public bool AutoCommit { get; internal set; }

    /// <summary>Public parameterless constructor for Dapper materialization.</summary>
    public EmailImportRule() { }

    /// <summary>
    /// Creates a rule after validating every invariant (transaction type, adapter requirements,
    /// priority, name bounds and regex compilation of <see cref="BodyRegex"/> and every string
    /// value inside <paramref name="fieldPatternsJson"/>).
    /// </summary>
    /// <exception cref="ArgumentException">Any invariant is violated.</exception>
    public static EmailImportRule Create(
        Guid userId,
        string name,
        string adapterKey,
        int priority,
        bool isEnabled,
        Guid accountId,
        Guid? categoryId,
        TransactionType transactionType,
        string currency,
        decimal? exchangeRate,
        string? fromAddress,
        string? subjectContains,
        string? bodyRegex,
        string? fieldPatternsJson,
        bool autoCommit)
    {
        Validate(name, adapterKey, priority, accountId, transactionType, currency,
            fromAddress, subjectContains, bodyRegex, fieldPatternsJson);

        return new EmailImportRule
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name.Trim(),
            AdapterKey = adapterKey.Trim(),
            Priority = priority,
            IsEnabled = isEnabled,
            AccountId = accountId,
            CategoryId = categoryId,
            TransactionType = transactionType,
            Currency = CurrencyCode.From(currency),
            ExchangeRate = NormalizeExchangeRate(exchangeRate),
            FromAddress = TrimOrNull(fromAddress),
            SubjectContains = TrimOrNull(subjectContains),
            BodyRegex = TrimOrNull(bodyRegex),
            FieldPatternsJson = TrimOrNull(fieldPatternsJson),
            AutoCommit = autoCommit
        };
    }

    /// <summary>
    /// Updates the rule in place with the same validation as <see cref="Create"/>.
    /// <see cref="UserId"/> is intentionally not a parameter: ownership never changes.
    /// Invalidates the lazily compiled <see cref="BodyRegex"/>.
    /// </summary>
    /// <exception cref="ArgumentException">Any invariant is violated.</exception>
    public void Update(
        string name,
        string adapterKey,
        int priority,
        bool isEnabled,
        Guid accountId,
        Guid? categoryId,
        TransactionType transactionType,
        string currency,
        decimal? exchangeRate,
        string? fromAddress,
        string? subjectContains,
        string? bodyRegex,
        string? fieldPatternsJson,
        bool autoCommit)
    {
        Validate(name, adapterKey, priority, accountId, transactionType, currency,
            fromAddress, subjectContains, bodyRegex, fieldPatternsJson);

        Name = name.Trim();
        AdapterKey = adapterKey.Trim();
        Priority = priority;
        IsEnabled = isEnabled;
        AccountId = accountId;
        CategoryId = categoryId;
        TransactionType = transactionType;
        Currency = CurrencyCode.From(currency);
        ExchangeRate = NormalizeExchangeRate(exchangeRate);
        FromAddress = TrimOrNull(fromAddress);
        SubjectContains = TrimOrNull(subjectContains);
        BodyRegex = TrimOrNull(bodyRegex);
        FieldPatternsJson = TrimOrNull(fieldPatternsJson);
        AutoCommit = autoCommit;
        _bodyRegex = null;
    }

    /// <summary>
    /// Evaluates the three rule filters (<see cref="FromAddress"/>, <see cref="SubjectContains"/>
    /// and <see cref="BodyRegex"/>) against an email, combined with AND. A null or empty filter
    /// never filters (it always passes). Pure domain logic consumed by the rule selection in
    /// <c>EmailParser</c>.
    /// </summary>
    /// <param name="fromAddress">Sender address; may be a raw header like <c>Name &lt;a@b.c&gt;</c>.</param>
    /// <param name="subject">Email subject.</param>
    /// <param name="plainText">Plain-text body the <see cref="BodyRegex"/> runs against.</param>
    public bool MatchesFilters(string? fromAddress, string? subject, string plainText)
    {
        if (!MatchesFromAddressFilter(fromAddress))
            return false;

        if (!string.IsNullOrWhiteSpace(SubjectContains) &&
            (subject is null || !subject.Contains(SubjectContains, StringComparison.OrdinalIgnoreCase)))
            return false;

        if (!string.IsNullOrWhiteSpace(BodyRegex) &&
            !CompiledBodyRegex.IsMatch(plainText ?? string.Empty))
            return false;

        return true;
    }

    private Regex CompiledBodyRegex =>
        _bodyRegex ??= new Regex(BodyRegex!, RegexOptions.IgnoreCase | RegexOptions.Multiline, RegexTimeout);

    private bool MatchesFromAddressFilter(string? fromAddress)
    {
        if (string.IsNullOrWhiteSpace(FromAddress))
            return true; // no filter

        // Single From-header parser (debt plan §6.2): blank sender → null → no match.
        var sender = EmailHeader.ExtractAddress(fromAddress);
        if (sender is null)
            return false;

        var filter = FromAddress.Trim();

        if (sender.Equals(filter, StringComparison.OrdinalIgnoreCase))
            return true;

        var senderDomain = DomainOf(sender);
        if (senderDomain is null)
            return false;

        var normalizedFilter = filter.StartsWith('@') ? filter[1..] : filter;
        if (normalizedFilter.Length == 0)
            return false;

        // ".es" style filter: pure domain suffix.
        if (normalizedFilter.StartsWith('.'))
            return senderDomain.EndsWith(normalizedFilter, StringComparison.OrdinalIgnoreCase);

        if (senderDomain.Equals(normalizedFilter, StringComparison.OrdinalIgnoreCase))
            return true;

        // Filter names a subdomain of the sender domain: "banco.santander.es" vs "santander.es".
        if (normalizedFilter.EndsWith("." + senderDomain, StringComparison.OrdinalIgnoreCase))
            return true;

        // Sender domain is a subdomain of the filter: "alerts.santander.es" vs "santander.es".
        return senderDomain.EndsWith("." + normalizedFilter, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Parses the part after '@' of an email address, tolerating header leftovers.</summary>
    private static string? DomainOf(string address)
    {
        var at = address.LastIndexOf('@');
        if (at < 0 || at == address.Length - 1)
            return null;

        var domain = address[(at + 1)..].Trim();
        var end = domain.IndexOfAny(['>', ' ', '\t', ';', ',']);
        if (end >= 0)
            domain = domain[..end].Trim();

        return domain.Length == 0 ? null : domain;
    }

    private static void Validate(
        string name,
        string adapterKey,
        int priority,
        Guid accountId,
        TransactionType transactionType,
        string currency,
        string? fromAddress,
        string? subjectContains,
        string? bodyRegex,
        string? fieldPatternsJson)
    {
        var normalizedName = Guard.NotNullOrEmpty(name, nameof(name)).Trim();
        if (normalizedName.Length > NameMaxLength)
            throw new ArgumentException($"Name cannot exceed {NameMaxLength} characters.", nameof(name));

        var normalizedAdapterKey = Guard.NotNullOrEmpty(adapterKey, nameof(adapterKey)).Trim();
        if (normalizedAdapterKey.Length > AdapterKeyMaxLength)
            throw new ArgumentException($"AdapterKey cannot exceed {AdapterKeyMaxLength} characters.", nameof(adapterKey));

        if (priority < 0)
            throw new ArgumentException("Priority cannot be negative.", nameof(priority));

        if (accountId == Guid.Empty)
            throw new ArgumentException("AccountId cannot be empty.", nameof(accountId));

        ValidateTransactionType(transactionType);

        // Throws ArgumentException if the currency is not a valid 3-letter code.
        _ = CurrencyCode.From(currency);

        ValidateOptionalLength(fromAddress, FromAddressMaxLength, nameof(fromAddress));
        ValidateOptionalLength(subjectContains, SubjectContainsMaxLength, nameof(subjectContains));
        ValidateOptionalLength(bodyRegex, BodyRegexMaxLength, nameof(bodyRegex));

        if (!string.IsNullOrWhiteSpace(bodyRegex))
            ValidateRegexCompiles(bodyRegex.Trim(), nameof(BodyRegex), "BodyRegex");

        ValidateFieldPatterns(fieldPatternsJson, normalizedAdapterKey);
    }

    /// <summary>
    /// Only Income and Expense are supported: an email cannot supply the second account a
    /// Transfer needs nor the person a Loan/Repayment needs.
    /// </summary>
    private static TransactionType ValidateTransactionType(TransactionType transactionType)
    {
        if (transactionType is not (TransactionType.Income or TransactionType.Expense))
            throw new ArgumentException(
                $"TransactionType '{transactionType}' is not supported by email import. " +
                "Only Income and Expense are allowed: an email cannot provide the second account " +
                "a Transfer requires nor the person a Loan or Repayment requires.",
                nameof(transactionType));
        return transactionType;
    }

    private static void ValidateFieldPatterns(string? fieldPatternsJson, string adapterKey)
    {
        var requiresPatterns = string.Equals(adapterKey, GenericRegexKey, StringComparison.Ordinal);
        var hasPatterns = !string.IsNullOrWhiteSpace(fieldPatternsJson);

        if (requiresPatterns && !hasPatterns)
            throw new ArgumentException(
                $"Adapter '{GenericRegexKey}' requires a non-empty FieldPatterns JSON.",
                nameof(fieldPatternsJson));

        if (!hasPatterns)
            return;

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(fieldPatternsJson!);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("FieldPatternsJson is not valid JSON.", nameof(fieldPatternsJson), ex);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new ArgumentException(
                    "FieldPatternsJson must be a JSON object whose values are regex strings " +
                    "(keys: amount, date, merchant, direction, symbol).",
                    nameof(fieldPatternsJson));

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String)
                    continue;

                var pattern = property.Value.GetString();
                if (pattern is null)
                    continue;

                ValidateRegexCompiles(pattern, nameof(fieldPatternsJson), $"FieldPatterns.{property.Name}");
            }
        }
    }

    private static void ValidateRegexCompiles(string pattern, string paramName, string description)
    {
        try
        {
            _ = new Regex(pattern, RegexOptions.None, RegexTimeout);
        }
        catch (ArgumentException ex)
        {
            throw new ArgumentException(
                $"{description} is not a valid regular expression: {ex.Message}", paramName, ex);
        }
    }

    private static void ValidateOptionalLength(string? value, int maxLength, string paramName)
    {
        if (value is not null && value.Trim().Length > maxLength)
            throw new ArgumentException($"{paramName} cannot exceed {maxLength} characters.", paramName);
    }

    private static decimal? NormalizeExchangeRate(decimal? exchangeRate) =>
        exchangeRate is null or <= 0 ? 1 : exchangeRate;

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
