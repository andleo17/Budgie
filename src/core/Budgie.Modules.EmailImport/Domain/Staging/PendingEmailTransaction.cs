using Budgie.Shared.Abstractions;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Enums;

namespace Budgie.Modules.EmailImport.Domain.Staging;

/// <summary>
/// Staging row for an email-derived transaction: either auto-committed into Finances or parked
/// in the review inbox. Idempotency relies on <c>UNIQUE(UserId, MessageId)</c> on top of
/// <c>Transactions.ExternalUid</c>; this entity never writes to Finances directly.
/// </summary>
public class PendingEmailTransaction : Entity
{
    /// <summary>Maximum length accepted for <see cref="Subject"/> (matches the DDL column).</summary>
    private const int SubjectMaxLength = 500;

    /// <summary>Maximum length accepted for <see cref="Merchant"/> (matches the DDL column).</summary>
    private const int MerchantMaxLength = 255;

    /// <summary>Maximum length accepted for <see cref="Notes"/> (matches the DDL column).</summary>
    private const int NotesMaxLength = 500;

    /// <summary>Maximum length accepted for <see cref="MessageId"/> (matches the DDL column).</summary>
    private const int MessageIdMaxLength = 255;

    /// <summary>Maximum length accepted for <see cref="ThreadId"/> (matches the DDL column).</summary>
    private const int ThreadIdMaxLength = 255;

    /// <summary>Maximum length accepted for <see cref="FailureReason"/> (matches the DDL column).</summary>
    private const int FailureReasonMaxLength = 500;

    public Guid Id { get; internal set; }
    public Guid UserId { get; internal set; }
    public Guid ConnectionId { get; internal set; }
    public Guid? RuleId { get; internal set; }
    public string MessageId { get; internal set; } = null!;
    public string? ThreadId { get; internal set; }
    public string? Subject { get; internal set; }
    public string? Merchant { get; internal set; }
    public string? Notes { get; internal set; }
    public Money Amount { get; internal set; } = null!;
    public CurrencyCode Currency { get; internal set; } = null!;
    public decimal ExchangeRate { get; internal set; } = 1;
    public DateTime Date { get; internal set; }
    public TransactionType TransactionType { get; internal set; }
    public decimal Confidence { get; internal set; }
    public string? FieldConfidenceJson { get; internal set; }
    public string? RawBody { get; internal set; }
    public PendingEmailStatus Status { get; internal set; }
    public Guid? CreatedTransactionId { get; internal set; }
    public string? FailureReason { get; internal set; }
    public DateTime CreatedAt { get; internal set; }
    public DateTime? ReviewedAt { get; internal set; }

    /// <summary>Public parameterless constructor for Dapper materialization.</summary>
    public PendingEmailTransaction() { }

    /// <summary>
    /// Creates a staging row in <see cref="PendingEmailStatus.Pending"/>.
    /// Text fields are truncated to the DDL limits (real emails carry very long subjects);
    /// out-of-range values are cut, never rejected. A blank <paramref name="merchant"/>
    /// normalizes to <c>null</c> (TrimOrNull, same rule as <c>EmailImportRule</c>), never "".
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="messageId"/> is null/empty; <paramref name="confidence"/> is outside [0, 1];
    /// the currency is not a valid 3-letter code; or <paramref name="amount"/> is not positive
    /// (<see cref="Money.FromPositive"/> rejects 0 and negatives, which the DDL also forbids).
    /// </exception>
    public static PendingEmailTransaction Create(
        Guid userId,
        Guid connectionId,
        Guid? ruleId,
        string messageId,
        string? threadId,
        string? subject,
        string? merchant,
        string? notes,
        decimal amount,
        string currency,
        decimal exchangeRate,
        DateTime date,
        TransactionType transactionType,
        decimal confidence,
        string? fieldConfidenceJson,
        string? rawBody,
        DateTime? createdAt = null)
    {
        var normalizedMessageId = Guard.NotNullOrEmpty(messageId, nameof(messageId)).Trim();
        if (normalizedMessageId.Length > MessageIdMaxLength)
            normalizedMessageId = normalizedMessageId[..MessageIdMaxLength];

        var currencyCode = CurrencyCode.From(currency);

        return new PendingEmailTransaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ConnectionId = connectionId,
            RuleId = ruleId,
            MessageId = normalizedMessageId,
            ThreadId = Truncate(threadId?.Trim(), ThreadIdMaxLength),
            Subject = Truncate(subject?.Trim(), SubjectMaxLength),
            Merchant = Truncate(TrimOrNull(merchant), MerchantMaxLength),
            Notes = Truncate(notes?.Trim(), NotesMaxLength),
            Amount = Money.FromPositive(amount, currencyCode),
            Currency = currencyCode,
            ExchangeRate = NormalizeExchangeRate(exchangeRate),
            Date = Guard.NormalizeDate(date),
            TransactionType = transactionType,
            Confidence = NormalizeConfidence(confidence, nameof(confidence)),
            FieldConfidenceJson = NormalizeOptionalJson(fieldConfidenceJson),
            RawBody = rawBody,
            Status = PendingEmailStatus.Pending,
            CreatedAt = createdAt ?? DateTime.UtcNow
        };
    }

    /// <summary>
    /// Marks the row as committed to Budgie, recording the created transaction id and the
    /// review timestamp. <paramref name="reviewedAt"/> is supplied by the caller (the handler)
    /// instead of reading the clock here, keeping the entity free of timezone assumptions.
    /// </summary>
    public void MarkCommitted(Guid transactionId, DateTime reviewedAt)
    {
        if (transactionId == Guid.Empty)
            throw new ArgumentException("Transaction id cannot be empty.", nameof(transactionId));

        Status = PendingEmailStatus.Committed;
        CreatedTransactionId = transactionId;
        ReviewedAt = reviewedAt;
    }

    /// <summary>
    /// Marks the row as dismissed by the user. The row is kept for traceability and so its
    /// MessageId keeps blocking re-import. <paramref name="reviewedAt"/> comes from the handler.
    /// </summary>
    public void MarkDismissed(DateTime reviewedAt)
    {
        Status = PendingEmailStatus.Dismissed;
        ReviewedAt = reviewedAt;
    }

    /// <summary>
    /// Marks the row as failed, truncating the reason to 500 characters.
    /// <paramref name="reviewedAt"/> comes from the handler.
    /// </summary>
    public void MarkFailed(string reason, DateTime reviewedAt)
    {
        Status = PendingEmailStatus.Failed;
        FailureReason = Truncate(reason, FailureReasonMaxLength);
        ReviewedAt = reviewedAt;
    }

    /// <summary>
    /// Persists user corrections from the review inbox. Amount, currency, exchange rate, date,
    /// merchant, notes and transaction type are replaced; <see cref="Status"/> stays
    /// <see cref="PendingEmailStatus.Pending"/> — committing is the handler's job through
    /// MarkCommitted, never a side effect of reviewing.
    /// </summary>
    /// <remarks>
    /// <paramref name="accountId"/> and <paramref name="categoryId"/> are not columns of the
    /// staging table: they belong to the eventual <c>CreateTransactionCommand</c> the approve
    /// handler dispatches. They are validated here so a review without a target account fails
    /// fast and visibly.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// <paramref name="accountId"/> is empty; <paramref name="transactionType"/> is not
    /// Income/Expense; the currency is invalid; or <paramref name="amount"/> is not positive.
    /// </exception>
    public void ApplyReview(
        decimal amount,
        string currency,
        decimal exchangeRate,
        DateTime date,
        string? merchant,
        string? notes,
        Guid accountId,
        Guid? categoryId,
        TransactionType transactionType)
    {
        if (accountId == Guid.Empty)
            throw new ArgumentException("AccountId cannot be empty.", nameof(accountId));

        if (transactionType is not (TransactionType.Income or TransactionType.Expense))
            throw new ArgumentException(
                $"TransactionType '{transactionType}' is not supported by email import. " +
                "Only Income and Expense are allowed.",
                nameof(transactionType));

        var currencyCode = CurrencyCode.From(currency);

        Amount = Money.FromPositive(amount, currencyCode);
        Currency = currencyCode;
        ExchangeRate = NormalizeExchangeRate(exchangeRate);
        Date = Guard.NormalizeDate(date);
        Merchant = Truncate(TrimOrNull(merchant), MerchantMaxLength);
        Notes = Truncate(notes?.Trim(), NotesMaxLength);
        TransactionType = transactionType;
        Status = PendingEmailStatus.Pending;
    }

    private static string? NormalizeOptionalJson(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>
    /// Trim-and-nullify normalization for <see cref="Merchant"/>: a blank merchant means
    /// "no merchant" (<c>null</c>), never an empty string — the same rule
    /// <c>EmailImportRule</c> applies to its optional text fields (debt plan §6.3).
    /// <see cref="Notes"/> deliberately keeps the older trim-to-empty behaviour: only
    /// Merchant was unified, on purpose.
    /// </summary>
    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static decimal NormalizeConfidence(decimal confidence, string paramName)
    {
        if (confidence < 0m || confidence > 1m)
            throw new ArgumentException($"{paramName} must be between 0 and 1.", paramName);
        return confidence;
    }

    private static decimal NormalizeExchangeRate(decimal exchangeRate) =>
        exchangeRate <= 0 ? 1 : exchangeRate;

    private static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];
}
