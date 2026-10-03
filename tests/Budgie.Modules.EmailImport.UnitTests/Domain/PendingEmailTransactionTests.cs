using Budgie.Modules.EmailImport.Domain.Staging;
using Budgie.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace Budgie.Modules.EmailImport.UnitTests.Domain;

/// <summary>
/// <see cref="PendingEmailTransaction"/> is the review row. Two invariants are load-bearing:
/// it REQUIRES a positive amount (the DDL column is NOT NULL, and the sync service relies on
/// it to know a message that failed to parse can have no staging row at all), and a review
/// never changes <see cref="Status"/> — only the approve handler may do that.
/// </summary>
public class PendingEmailTransactionTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ConnectionId = Guid.NewGuid();

    private static PendingEmailTransaction Create(
        string messageId = "msg-1",
        decimal amount = 45.20m,
        string currency = "EUR",
        decimal exchangeRate = 1m,
        TransactionType type = TransactionType.Expense,
        decimal confidence = 0.9m,
        string? subject = "Su pago",
        string? merchant = "Cafetería Central",
        string? notes = null,
        Guid? ruleId = null) =>
        PendingEmailTransaction.Create(
            userId: UserId,
            connectionId: ConnectionId,
            ruleId: ruleId,
            messageId: messageId,
            threadId: "thread-1",
            subject: subject,
            merchant: merchant,
            notes: notes,
            amount: amount,
            currency: currency,
            exchangeRate: exchangeRate,
            date: new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Unspecified),
            transactionType: type,
            confidence: confidence,
            fieldConfidenceJson: """{"amount":1.0}""",
            rawBody: "cuerpo original",
            createdAt: new DateTime(2026, 10, 2, 9, 0, 0, DateTimeKind.Utc));

    // ── Creation ───────────────────────────────────────────────────────────────────────
    [Fact]
    public void Create_StagedRow_StartsPendingWithNoLinkedTransaction()
    {
        var pending = Create();

        pending.Id.Should().NotBeEmpty();
        pending.UserId.Should().Be(UserId);
        pending.ConnectionId.Should().Be(ConnectionId);
        pending.MessageId.Should().Be("msg-1");
        pending.Status.Should().Be(PendingEmailStatus.Pending);
        pending.CreatedTransactionId.Should().BeNull();
        pending.ReviewedAt.Should().BeNull();
        pending.FailureReason.Should().BeNull();
        pending.Amount.Amount.Should().Be(45.20m);
        pending.Currency.Code.Should().Be("EUR");
        pending.TransactionType.Should().Be(TransactionType.Expense);
        pending.Confidence.Should().Be(0.9m);
        pending.RawBody.Should().Be("cuerpo original");
    }

    [Fact]
    public void Create_MessageIdIsTrimmedAndTruncatedToTheDdlLimit()
    {
        // Real providers carry long message ids; the DDL column is VARCHAR(255).
        var pending = Create(messageId: new string('m', 300));

        pending.MessageId.Should().HaveLength(255);
    }

    [Fact]
    public void Create_EmptyMessageId_IsRejected()
    {
        // It is half of the idempotency key: without it nothing can be deduplicated.
        var act = () => Create(messageId: "   ");
        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("messageId");
    }

    [Fact]
    public void Create_SubjectOverFiveHundredCharacters_IsTruncatedNotRejected()
    {
        // Real emails carry very long subjects; truncating beats failing the sync.
        Create(subject: new string('s', 600)).Subject.Should().HaveLength(500);
    }

    [Fact]
    public void Create_MerchantOverTwoHundredFiftyFiveCharacters_IsTruncated()
    {
        Create(merchant: new string('m', 300)).Merchant.Should().HaveLength(255);
    }

    [Fact]
    public void Create_BlankNotesAndMerchant_BecomeEmptyAfterTrimming()
    {
        // Trimmed, NOT nulled: TrimOrNull's blank-to-null rule belongs to the rule entity,
        // not here. Downstream consumers must treat "" as "no merchant", which the approve
        // path already does (`Notes: pending.Merchant` is then an empty note, not a lie).
        var pending = Create(merchant: "   ", notes: "   ");

        pending.Merchant.Should().BeEmpty();
        pending.Notes.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.01)]
    public void Create_NonPositiveAmount_IsRejected(decimal amount)
    {
        // A row with no amount is not a transaction. This is also why a message that fails
        // to parse can never produce a staging row.
        var act = () => Create(amount: amount);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_InvalidCurrency_IsRejected()
    {
        var act = () => Create(currency: "EURO");
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.5)]
    public void Create_NonPositiveExchangeRate_NormalizesToOne(decimal rate)
    {
        Create(exchangeRate: rate).ExchangeRate.Should().Be(1);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void Create_ConfidenceOutsideZeroToOne_IsRejected(decimal confidence)
    {
        var act = () => Create(confidence: confidence);
        act.Should().Throw<ArgumentException>().Which.Message.Should().Contain("between 0 and 1");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Create_ConfidenceAtTheBoundary_IsAccepted(decimal confidence)
    {
        var act = () => Create(confidence: confidence);
        act.Should().NotThrow();
    }

    [Fact]
    public void Create_DateIsNormalizedToUnspecified()
    {
        // The staging column is TIMESTAMP WITHOUT TIME ZONE; a Local kind would shift.
        var pending = PendingEmailTransaction.Create(
            UserId, ConnectionId, null, "m", null, null, null, null,
            10m, "EUR", 1m,
            new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Local),
            TransactionType.Expense, 1m, null, null);

        pending.Date.Kind.Should().Be(DateTimeKind.Unspecified);
        pending.Date.Should().Be(new DateTime(2026, 10, 1, 12, 0, 0));
    }

    // ── State transitions ─────────────────────────────────────────────────────────────
    [Fact]
    public void MarkCommitted_LinksTheTransactionAndStampsTheReview()
    {
        var pending = Create();
        var transactionId = Guid.NewGuid();
        var reviewedAt = new DateTime(2026, 10, 2, 10, 0, 0, DateTimeKind.Utc);

        pending.MarkCommitted(transactionId, reviewedAt);

        pending.Status.Should().Be(PendingEmailStatus.Committed);
        pending.CreatedTransactionId.Should().Be(transactionId);
        pending.ReviewedAt.Should().Be(reviewedAt);
    }

    [Fact]
    public void MarkCommitted_EmptyTransactionId_IsRejected()
    {
        // Guid.Empty would violate the FK on "CreatedTransactionId" and break the very
        // 23505 barrier this whole design relies on.
        var pending = Create();

        var act = () => pending.MarkCommitted(Guid.Empty, DateTime.UtcNow);

        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("transactionId");
        pending.Status.Should().Be(PendingEmailStatus.Pending);
    }

    [Fact]
    public void MarkDismissed_MovesToDismissedAndKeepsTheRow()
    {
        // Never a delete: the MessageId must keep blocking re-import of the same email.
        var pending = Create();

        pending.MarkDismissed(DateTime.UtcNow);

        pending.Status.Should().Be(PendingEmailStatus.Dismissed);
        pending.MessageId.Should().Be("msg-1");
        pending.CreatedTransactionId.Should().BeNull();
    }

    [Fact]
    public void MarkFailed_StoresTheReasonAndTruncatesIt()
    {
        var pending = Create();

        pending.MarkFailed(new string('r', 600), DateTime.UtcNow);

        pending.Status.Should().Be(PendingEmailStatus.Failed);
        pending.FailureReason.Should().HaveLength(500);
        pending.ReviewedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkFailed_ReasonIsTheOneTheUiShows()
    {
        const string reason = "Transaction.BeforeControlStartDate";
        var pending = Create();

        pending.MarkFailed(reason, DateTime.UtcNow);

        pending.FailureReason.Should().Be(reason);
    }

    // ── Review corrections ────────────────────────────────────────────────────────────
    [Fact]
    public void ApplyReview_ReplacesTheCorrectedFieldsAndKeepsStatusPending()
    {
        var pending = Create();
        var accountId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        pending.ApplyReview(
            amount: 50m, currency: "EUR", exchangeRate: 1.1m,
            date: new DateTime(2026, 9, 30), merchant: "Otro comercio", notes: "corregido",
            accountId, categoryId, TransactionType.Income);

        pending.Amount.Amount.Should().Be(50m);
        pending.Currency.Code.Should().Be("EUR");
        pending.ExchangeRate.Should().Be(1.1m);
        pending.Date.Should().Be(new DateTime(2026, 9, 30));
        pending.Merchant.Should().Be("Otro comercio");
        pending.Notes.Should().Be("corregido");
        pending.TransactionType.Should().Be(TransactionType.Income);
        // Status stays Pending: committing is the handler's job, never a side effect.
        pending.Status.Should().Be(PendingEmailStatus.Pending);
        pending.CreatedTransactionId.Should().BeNull();
    }

    [Fact]
    public void ApplyReview_NullMerchantAndNotes_ClearThem()
    {
        var pending = Create(notes: "notas previas");

        pending.ApplyReview(
            10m, "EUR", 1m, DateTime.UtcNow, merchant: null, notes: null,
            Guid.NewGuid(), null, TransactionType.Expense);

        pending.Merchant.Should().BeNull();
        pending.Notes.Should().BeNull();
    }

    [Fact]
    public void ApplyReview_EmptyAccountId_IsRejected()
    {
        var pending = Create();

        var act = () => pending.ApplyReview(
            10m, "EUR", 1m, DateTime.UtcNow, null, null, Guid.Empty, null, TransactionType.Expense);

        act.Should().Throw<ArgumentException>().Which.Message.Should().Contain("AccountId");
    }

    [Theory]
    [InlineData(TransactionType.Transfer)]
    [InlineData(TransactionType.Loan)]
    [InlineData(TransactionType.Repayment)]
    public void ApplyReview_UnsupportedTransactionType_IsRejected(TransactionType type)
    {
        var pending = Create();

        var act = () => pending.ApplyReview(
            10m, "EUR", 1m, DateTime.UtcNow, null, null, Guid.NewGuid(), null, type);

        act.Should().Throw<ArgumentException>().Which.Message.Should().Contain("not supported by email import");
        pending.TransactionType.Should().Be(TransactionType.Expense);  // nothing changed
    }

    [Fact]
    public void ApplyReview_NonPositiveAmount_IsRejected()
    {
        var pending = Create();

        var act = () => pending.ApplyReview(
            0m, "EUR", 1m, DateTime.UtcNow, null, null, Guid.NewGuid(), null, TransactionType.Expense);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ApplyReview_InvalidCurrency_IsRejected()
    {
        var pending = Create();

        var act = () => pending.ApplyReview(
            10m, "EURO", 1m, DateTime.UtcNow, null, null, Guid.NewGuid(), null, TransactionType.Expense);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ApplyReview_AppliedToACommittedRow_CanOnlyReopenItAsPending()
    {
        // Documented behaviour: reviewing resets the status. The approve handler's Status
        // guard is what prevents that from being exploitable.
        var pending = Create();
        pending.MarkCommitted(Guid.NewGuid(), DateTime.UtcNow);

        pending.ApplyReview(
            10m, "EUR", 1m, DateTime.UtcNow, null, null, Guid.NewGuid(), null, TransactionType.Expense);

        pending.Status.Should().Be(PendingEmailStatus.Pending);
    }

    // ── Status enum: these values travel as NUMBERS on the wire ────────────────────────
    [Fact]
    public void PendingEmailStatus_HasTheDocumentedNumericValues()
    {
        // BudgieJsonContext does not use JsonStringEnumConverter, so the wire contract is
        // the ordinal. Changing an order would silently break every stored row and client.
        ((int)PendingEmailStatus.Pending).Should().Be(0);
        ((int)PendingEmailStatus.Committed).Should().Be(1);
        ((int)PendingEmailStatus.Dismissed).Should().Be(2);
        ((int)PendingEmailStatus.Failed).Should().Be(3);
    }

    [Fact]
    public void ParameterlessConstructor_LeavesEverythingAtItsDefault()
    {
        // The public parameterless constructor exists for Dapper materialization.
        var act = () => new PendingEmailTransaction();

        act.Should().NotThrow();
        new PendingEmailTransaction().Status.Should().Be(PendingEmailStatus.Pending);
    }
}
