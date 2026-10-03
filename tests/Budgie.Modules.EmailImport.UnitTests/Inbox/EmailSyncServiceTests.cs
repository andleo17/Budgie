using System.Text.Json;
using Budgie.Modules.EmailImport.Application;
using Budgie.Modules.EmailImport.Application.Abstractions;
using Budgie.Modules.EmailImport.Application.Inbox;
using Budgie.Modules.EmailImport.Application.Inbox.Abstractions;
using Budgie.Modules.EmailImport.Application.Parsing;
using Budgie.Modules.EmailImport.Application.Parsing.Adapters;
using Budgie.Modules.EmailImport.Domain.Connections;
using Budgie.Modules.EmailImport.Domain.Parsing;
using Budgie.Modules.EmailImport.Domain.Rules;
using Budgie.Modules.EmailImport.Domain.Staging;
using Budgie.Modules.EmailImport.Infrastructure.Parsing;
using Budgie.Modules.EmailImport.UnitTests.TestDoubles;
using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Application.Transactions.CreateTransaction;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Enums;
using ErrorOr;
using FluentAssertions;
using Mediator;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Npgsql;
using Xunit;

namespace Budgie.Modules.EmailImport.UnitTests.Inbox;

/// <summary>
/// <see cref="EmailSyncService"/> is where the module's two idempotency barriers live, and
/// where one bad email must never take down a whole mailbox (or another user's).
/// <para>The load-bearing assertions use <c>Received.InOrder</c>, not call counts: the cursor
/// is persisted exactly once and only AFTER every message of the batch, and the staging
/// insert happens BEFORE any commit attempt.</para>
/// </summary>
public class EmailSyncServiceTests
{
    private readonly IEmailConnectionRepository _connections = Substitute.For<IEmailConnectionRepository>();
    private readonly IEmailImportRuleRepository _rules = Substitute.For<IEmailImportRuleRepository>();
    private readonly IPendingEmailTransactionRepository _pending = Substitute.For<IPendingEmailTransactionRepository>();
    private readonly IEmailProviderResolver _resolver = Substitute.For<IEmailProviderResolver>();
    private readonly ITransactionRepository _transactions = Substitute.For<ITransactionRepository>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly ICurrentUserProvider _currentUser = Substitute.For<ICurrentUserProvider>();
    private readonly ICurrentUserProviderAccessor _accessor = Substitute.For<ICurrentUserProviderAccessor>();
    private readonly ILanguageProvider _language = Substitute.For<ILanguageProvider>();
    private readonly FakeEmailProviderClient _provider = new();
    private readonly EmailSyncService _service;

    /// <summary>Every <see cref="CreateTransactionCommand"/> the service dispatched, in order.</summary>
    private readonly List<CreateTransactionCommand> _sent = [];

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid CategoryId = Guid.NewGuid();

    public EmailSyncServiceTests()
    {
        _currentUser.GetUserId().Returns(UserId);
        _language.LanguageCode.Returns("es");
        _resolver.Resolve(Arg.Any<EmailProvider>()).Returns(_provider);
        _pending.TryAddAsync(Arg.Any<PendingEmailTransaction>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _service = new EmailSyncService(
            _connections, _rules, _pending, _resolver,
            new EmailParser(new MessageTextExtractor(),
                new BankAdapterRegistry([new GenericRegexAdapter()])),
            _transactions, _mediator, _accessor, _language,
            Substitute.For<ILogger<EmailSyncService>>());
    }

    // ── Fixtures ───────────────────────────────────────────────────────────────────────
    private EmailConnection Connection(decimal threshold = 0.85m)
    {
        var connection = EmailConnection.Create(
            UserId, EmailProvider.Gmail, "usuario@banco.es", "v1:bm9uY2U=:Y2lwaGVy:dGFn", threshold);
        connection.AdvanceCursor("cursor-inicial");
        return connection;
    }

    private EmailImportRule Rule(bool autoCommit = true, string? subjectContains = null) =>
        EmailImportRule.Create(
            UserId, "Banco Ejemplo", EmailImportRule.GenericRegexKey, 0, true,
            AccountId, CategoryId, TransactionType.Expense, "EUR", null,
            null, subjectContains, null,
            JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["amount"] = @"Importe:\s*(?<amount>[\d.,]+)",
                ["date"] = @"Fecha:\s*(?<date>\d{2}/\d{2}/\d{4})",
                ["merchant"] = @"Comercio:\s*(?<merchant>.+)"
            }),
            autoCommit);

    private static EmailMessage Message(string messageId, string amount = "45,20") =>
        EmailMessage.Create(
            messageId, "thread-1", "notificaciones@banco.es", "Su pago",
            $"Fecha: 01/10/2026\nImporte: {amount} €\nComercio: Cafetería Central",
            new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero));

    private void GivenConnections(params EmailConnection[] connections) =>
        _connections.GetAllAsync(UserId, Arg.Any<CancellationToken>()).Returns([.. connections]);

    private void GivenRules(params EmailImportRule[] rules) =>
        _rules.GetByUserAsync(UserId, Arg.Any<CancellationToken>()).Returns([.. rules]);

    /// <summary>Makes the mediator succeed, recording every dispatched command.</summary>
    private void CommitsSucceed() => CommitsReturn(_ => Guid.NewGuid());

    /// <summary>Makes the dispatch use <paramref name="result"/>, recording each command.</summary>
    private void CommitsReturn(Func<CreateTransactionCommand, ErrorOr<Guid>> result) =>
        _mediator.Send(Arg.Do<CreateTransactionCommand>(_sent.Add), Arg.Any<CancellationToken>())
            .Returns(call => new ValueTask<ErrorOr<Guid>>(result(call.Arg<CreateTransactionCommand>())));

    /// <summary>Makes the dispatch itself throw — not return an error.</summary>
    private void CommitsThrow(Exception exception) =>
        _mediator.Send(Arg.Do<CreateTransactionCommand>(_sent.Add), Arg.Any<CancellationToken>())
            .Throws(exception);

    private CreateTransactionCommand SentCommand() => _sent.Should().ContainSingle().Subject;

    /// <summary>
    /// A real <see cref="PostgresException"/> carrying <c>SqlState = 23505</c>.
    /// <c>PostgresException</c> has no public constructor (it is built by the protocol
    /// handler), so the internal setters are reached through reflection — which is exactly
    /// why the sync service matches on <c>ex.SqlState</c> rather than on the message.
    /// </summary>
    private static PostgresException UniqueViolation()
    {
        var exception = (PostgresException)System.Runtime.CompilerServices.RuntimeHelpers
            .GetUninitializedObject(typeof(PostgresException));
        // SqlState is an internal get-only property with a backing field; the property
        // itself cannot be assigned, so the field is written directly.
        typeof(PostgresException)
            .GetField("<SqlState>k__BackingField",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(exception, PostgresErrorCodes.UniqueViolation);
        return exception;
    }

    // ── The happy path ─────────────────────────────────────────────────────────────────
    [Fact]
    public async Task SyncUserAsync_HighConfidenceAutoCommitRule_CommitsAndStampsTheCursor()
    {
        var connection = Connection();
        _provider.Messages.Add(Message("msg-1"));
        _provider.CursorToEstablish = "cursor-nuevo";
        GivenConnections(connection);
        GivenRules(Rule(autoCommit: true));
        CommitsSucceed();

        var result = await _service.SyncUserAsync(UserId, CancellationToken.None);

        result.MessagesFetched.Should().Be(1);
        result.AutoCommitted.Should().Be(1);
        result.Staged.Should().Be(0);
        result.Failed.Should().Be(0);
        result.ConnectionErrors.Should().BeEmpty();
        connection.SyncCursor.Should().Be("cursor-nuevo");
        connection.LastError.Should().BeNull();
        connection.LastSyncAt.Should().NotBeNull();
        await _connections.Received(1).UpdateAsync(connection, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncUserAsync_SuccessfulCommit_BuildsTheDocumentedExternalUid()
    {
        // This exact string IS the second idempotency barrier: the sync worker and the manual
        // approval MUST build it byte for byte identically, or the unique index would never
        // see the collision and one email could be imported twice.
        var connection = Connection();
        _provider.Messages.Add(Message("msg-1"));
        GivenConnections(connection);
        GivenRules(Rule());
        CommitsSucceed();

        await _service.SyncUserAsync(UserId, CancellationToken.None);

        SentCommand().ExternalUid.Should().Be("email:gmail:msg-1");
    }

    [Fact]
    public async Task SyncUserAsync_SuccessfulCommit_CarriesTheRuleTargetsAndTheExtractedValues()
    {
        var connection = Connection();
        _provider.Messages.Add(Message("msg-1", amount: "1234,56"));
        GivenConnections(connection);
        GivenRules(Rule());
        CommitsSucceed();

        await _service.SyncUserAsync(UserId, CancellationToken.None);

        var command = SentCommand();
        command.AccountId.Should().Be(AccountId);
        command.CategoryId.Should().Be(CategoryId);
        command.Amount.Should().Be(1234.56m);
        command.Currency.Should().Be("EUR");
        command.ExchangeRate.Should().Be(1m);
        command.Date.Should().Be(new DateTime(2026, 10, 1));
        command.Type.Should().Be(TransactionType.Expense);
        command.Notes.Should().BeNull();   // auto-commit sends no notes: nobody vouched for it
    }

    [Fact]
    public async Task SyncUserAsync_SuccessfulCommit_SendsTheExtractedDateNotTheReceiptDate()
    {
        var connection = Connection();
        _provider.Messages.Add(Message("msg-1"));
        GivenConnections(connection);
        GivenRules(Rule());
        CommitsSucceed();

        await _service.SyncUserAsync(UserId, CancellationToken.None);

        SentCommand().Date.Should().Be(new DateTime(2026, 10, 1));
    }

    // ── Nothing is invented when nothing matches ───────────────────────────────────────
    [Fact]
    public async Task SyncUserAsync_NoRuleMatches_CountsAsSkippedAndPersistsNothing()
    {
        // spec 13 decision: a message with no matching rule is never persisted — such a row
        // would need a nullable amount the DDL deliberately forbids.
        var connection = Connection();
        _provider.Messages.Add(Message("msg-1"));
        GivenConnections(connection);
        GivenRules(Rule(autoCommit: true, subjectContains: "Reembolso"));   // will not match

        var result = await _service.SyncUserAsync(UserId, CancellationToken.None);

        result.Skipped.Should().Be(1);
        result.Staged.Should().Be(0);
        result.AutoCommitted.Should().Be(0);
        await _pending.DidNotReceive()
            .TryAddAsync(Arg.Any<PendingEmailTransaction>(), Arg.Any<CancellationToken>());
        _sent.Should().BeEmpty();
    }

    [Fact]
    public async Task SyncUserAsync_RuleWithoutAutoCommit_LeavesTheRowPendingAndNeverDispatches()
    {
        var connection = Connection();
        _provider.Messages.Add(Message("msg-1"));
        GivenConnections(connection);
        GivenRules(Rule(autoCommit: false));
        CommitsSucceed();

        var result = await _service.SyncUserAsync(UserId, CancellationToken.None);

        result.Staged.Should().Be(1);
        result.AutoCommitted.Should().Be(0);
        _sent.Should().BeEmpty();
        await _pending.Received(1)
            .TryAddAsync(Arg.Any<PendingEmailTransaction>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncUserAsync_ConfidenceBelowTheConnectionThreshold_LeavesTheRowPending()
    {
        // The rule wants to auto-commit but this connection demands a perfect 1.0. The
        // connection's own threshold wins.
        // The rule wants to auto-commit, but this connection demands 0.95 while a message
        // with no parseable date only reaches ~0.85. The connection's own threshold wins.
        var connection = Connection(threshold: 0.95m);
        _provider.Messages.Add(EmailMessage.Create(
            "msg-1", "thread-1", "notificaciones@banco.es", "Su pago",
            "Importe: 45,20 €\nComercio: Cafetería Central",     // no "Fecha:" line
            new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero)));
        GivenConnections(connection);
        GivenRules(Rule(autoCommit: true));
        CommitsSucceed();

        var result = await _service.SyncUserAsync(UserId, CancellationToken.None);

        result.Staged.Should().Be(1);
        result.AutoCommitted.Should().Be(0);
        _sent.Should().BeEmpty();
    }

    [Fact]
    public async Task SyncUserAsync_SyncDisabledConnection_IsNotProcessedAtAll()
    {
        var connection = Connection();
        connection.Disable();
        _provider.Messages.Add(Message("msg-1"));
        GivenConnections(connection);
        GivenRules(Rule());

        var result = await _service.SyncUserAsync(UserId, CancellationToken.None);

        result.ConnectionsProcessed.Should().Be(0);
        _provider.ListCallCount.Should().Be(0);
    }

    [Fact]
    public async Task SyncUserAsync_NoConnections_ReturnsAnEmptyResult()
    {
        GivenConnections();

        var result = await _service.SyncUserAsync(UserId, CancellationToken.None);

        result.ConnectionsProcessed.Should().Be(0);
        result.MessagesFetched.Should().Be(0);
        result.AutoCommitted.Should().Be(0);
        result.Staged.Should().Be(0);
        result.Skipped.Should().Be(0);
        result.Failed.Should().Be(0);
        result.ConnectionErrors.Should().BeEmpty();
    }

    // ── IDEMPOTENCY BARRIER 1: the staging insert ─────────────────────────────────────
    [Fact]
    public async Task SyncUserAsync_StagingInsertReturnsFalse_NeverDispatchesCreateTransaction()
    {
        // The re-read after a crash: the row already exists, so the message is a duplicate
        // and no transaction may be created from it. This is the case that prevents the same
        // email being imported twice when the cursor was never persisted.
        var connection = Connection();
        _provider.Messages.Add(Message("msg-1"));
        GivenConnections(connection);
        GivenRules(Rule());
        CommitsSucceed();
        _pending.TryAddAsync(Arg.Any<PendingEmailTransaction>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await _service.SyncUserAsync(UserId, CancellationToken.None);

        _sent.Should().BeEmpty();
        result.AutoCommitted.Should().Be(0);
        result.Staged.Should().Be(0);
        result.MessagesFetched.Should().Be(1);   // it was still counted as fetched
        result.Failed.Should().Be(0);
    }

    [Fact]
    public async Task SyncUserAsync_StagingInsertHappensBeforeAnyCommitAttempt()
    {
        // ORDER matters: the staging INSERT is the FIRST write of this message's pipeline,
        // before any commit attempt at all.
        var connection = Connection();
        _provider.Messages.Add(Message("msg-1"));
        GivenConnections(connection);
        GivenRules(Rule());
        CommitsSucceed();

        await _service.SyncUserAsync(UserId, CancellationToken.None);

        Received.InOrder(() =>
        {
            _pending.TryAddAsync(Arg.Any<PendingEmailTransaction>(), Arg.Any<CancellationToken>());
            _mediator.Send(Arg.Any<CreateTransactionCommand>(), Arg.Any<CancellationToken>());
            _pending.MarkCommittedAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(),
                Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
            _connections.UpdateAsync(Arg.Any<EmailConnection>(), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task SyncUserAsync_DuplicateSecondMessage_OnlyTheNewOneIsCommitted()
    {
        var connection = Connection();
        _provider.Messages.Add(Message("msg-1"));
        _provider.Messages.Add(Message("msg-2"));
        GivenConnections(connection);
        GivenRules(Rule());
        CommitsSucceed();
        _pending.TryAddAsync(
                Arg.Is<PendingEmailTransaction>(p => p.MessageId == "msg-2"),
                Arg.Any<CancellationToken>())
            .Returns(false);

        await _service.SyncUserAsync(UserId, CancellationToken.None);

        SentCommand().ExternalUid.Should().Be("email:gmail:msg-1");
    }

    // ── IDEMPOTENCY BARRIER 2: the ExternalUid unique index ───────────────────────────
    [Fact]
    public async Task SyncUserAsync_UniqueViolationOnTheTransaction_StampsTheExistingTransaction()
    {
        // SqlState 23505 means "it was ALREADY committed" — a success, not an error.
        var existingId = Guid.NewGuid();
        var connection = Connection();
        _provider.Messages.Add(Message("msg-1"));
        GivenConnections(connection);
        GivenRules(Rule());
        CommitsThrow(UniqueViolation());
        _transactions.GetIdByExternalUidAsync(
                UserId, "email:gmail:msg-1", Arg.Any<CancellationToken>())
            .Returns(existingId);

        var result = await _service.SyncUserAsync(UserId, CancellationToken.None);

        result.AutoCommitted.Should().Be(1);          // committed, NOT failed
        result.Failed.Should().Be(0);
        result.ConnectionErrors.Should().BeEmpty();
        await _pending.Received(1).MarkCommittedAsync(
            UserId, Arg.Any<Guid>(), existingId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        await _pending.DidNotReceive().MarkFailedAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(),
            Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncUserAsync_UniqueViolationDoesNotPropagate_AndTheCursorStillAdvances()
    {
        var connection = Connection();
        _provider.Messages.Add(Message("msg-1"));
        _provider.CursorToEstablish = "cursor-nuevo";
        GivenConnections(connection);
        GivenRules(Rule());
        CommitsThrow(UniqueViolation());
        _transactions.GetIdByExternalUidAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());

        var act = () => _service.SyncUserAsync(UserId, CancellationToken.None);

        await act.Should().NotThrowAsync();
        connection.SyncCursor.Should().Be("cursor-nuevo");
    }

    [Fact]
    public async Task SyncUserAsync_UniqueViolationButTheTransactionVanished_MarksTheRowFailed()
    {
        // Defensive race: it existed at insert time and is gone now. Never invent a link.
        var connection = Connection();
        _provider.Messages.Add(Message("msg-1"));
        GivenConnections(connection);
        GivenRules(Rule());
        CommitsThrow(UniqueViolation());
        _transactions.GetIdByExternalUidAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Guid?)null);

        var result = await _service.SyncUserAsync(UserId, CancellationToken.None);

        result.Failed.Should().Be(1);
        result.AutoCommitted.Should().Be(0);
        await _pending.Received(1).MarkFailedAsync(
            UserId, Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        await _pending.DidNotReceive().MarkCommittedAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(),
            Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    // ── Errors from the Finances command ──────────────────────────────────────────────
    [Fact]
    public async Task SyncUserAsync_CreateTransactionReturnsAnError_MarksTheRowFailedWithThatDescription()
    {
        // The row carries the very description the UI shows, so the user can correct it
        // instead of the import failing silently on every sync.
        const string description = "The transaction date is before the account control start date.";
        var connection = Connection();
        _provider.Messages.Add(Message("msg-1"));
        GivenConnections(connection);
        GivenRules(Rule());
        CommitsReturn(_ => Error.Validation("Transaction.BeforeControlStartDate", description));

        var result = await _service.SyncUserAsync(UserId, CancellationToken.None);

        result.Failed.Should().Be(1);
        await _pending.Received(1).MarkFailedAsync(
            UserId, Arg.Any<Guid>(), description, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncUserAsync_DispatchThrowsAnyOtherException_MarksTheRowFailedAndContinues()
    {
        var connection = Connection();
        _provider.Messages.Add(Message("msg-1"));
        _provider.Messages.Add(Message("msg-2"));
        GivenConnections(connection);
        GivenRules(Rule());
        CommitsThrow(new InvalidOperationException("boom"));

        var result = await _service.SyncUserAsync(UserId, CancellationToken.None);

        // BOTH messages got a staging row and both failed — the batch did not abort.
        result.Failed.Should().Be(2);
        result.MessagesFetched.Should().Be(2);
        _sent.Should().HaveCount(2);
        await _pending.Received(2).MarkFailedAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), "boom", Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    // ── The cursor ────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task SyncUserAsync_PersistsTheCursorExactlyOnceAndOnlyAtTheEnd()
    {
        // INVARIANT #1. If the cursor were saved inside the loop, a crash mid-batch would
        // silently skip every message not yet processed.
        var connection = Connection();
        _provider.Messages.Add(Message("msg-1"));
        _provider.Messages.Add(Message("msg-2"));
        _provider.Messages.Add(Message("msg-3"));
        _provider.CursorToEstablish = "cursor-nuevo";
        GivenConnections(connection);
        GivenRules(Rule(autoCommit: false));   // staging only: no commit calls to interleave

        await _service.SyncUserAsync(UserId, CancellationToken.None);

        await _connections.Received(1).UpdateAsync(connection, Arg.Any<CancellationToken>());

        Received.InOrder(() =>
        {
            _pending.TryAddAsync(Arg.Any<PendingEmailTransaction>(), Arg.Any<CancellationToken>());
            _pending.TryAddAsync(Arg.Any<PendingEmailTransaction>(), Arg.Any<CancellationToken>());
            _pending.TryAddAsync(Arg.Any<PendingEmailTransaction>(), Arg.Any<CancellationToken>());
            _connections.UpdateAsync(Arg.Any<EmailConnection>(), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task SyncUserAsync_SuccessfulPass_RecordsSuccessWithTheSkipCount()
    {
        var connection = Connection();
        _provider.Messages.Add(Message("msg-1"));
        GivenConnections(connection);
        GivenRules(Rule(autoCommit: false));

        await _service.SyncUserAsync(UserId, CancellationToken.None);

        connection.LastSkippedCount.Should().Be(0);
        connection.LastSyncAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SyncUserAsync_ReReadingTheSameMessagesAfterACrash_CommitsNothingNew()
    {
        // The end-to-end shape of barrier #1: same messages, TryAdd refuses, no transaction.
        var connection = Connection();
        _provider.Messages.Add(Message("msg-1"));
        GivenConnections(connection);
        GivenRules(Rule());
        CommitsSucceed();
        _pending.TryAddAsync(Arg.Any<PendingEmailTransaction>(), Arg.Any<CancellationToken>())
            .Returns(true, false);   // first pass inserts, the replay does not

        await _service.SyncUserAsync(UserId, CancellationToken.None);
        var afterFirstPass = _sent.Count;
        await _service.SyncUserAsync(UserId, CancellationToken.None);

        afterFirstPass.Should().Be(1);
        _sent.Should().HaveCount(1);
    }

    // ── Provider failures ─────────────────────────────────────────────────────────────
    [Fact]
    public async Task SyncUserAsync_UnauthorizedProviderError_DisablesTheConnectionAndRecordsTheError()
    {
        // A revoked credential must stop THIS connection only.
        var connection = Connection();
        _provider.ListFailure =
            new EmailProviderException(EmailProviderError.Unauthorized, "invalid_grant");
        GivenConnections(connection);
        GivenRules(Rule());
        CommitsSucceed();

        var result = await _service.SyncUserAsync(UserId, CancellationToken.None);

        connection.SyncEnabled.Should().BeFalse();
        connection.LastError.Should().NotBeNullOrWhiteSpace();
        result.ConnectionErrors.Should().ContainSingle();
        result.MessagesFetched.Should().Be(0);
        _sent.Should().BeEmpty();
        await _connections.Received(1).UpdateAsync(connection, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncUserAsync_UnauthorizedProviderError_IsReportedInTheUserLanguage()
    {
        // The message is stored on the connection and shown in the UI, so it is localized.
        var connection = Connection();
        _provider.ListFailure =
            new EmailProviderException(EmailProviderError.Unauthorized, "invalid_grant");
        GivenConnections(connection);
        GivenRules(Rule());

        await _service.SyncUserAsync(UserId, CancellationToken.None);

        connection.LastError.Should().Contain("credencial");
    }

    [Theory]
    [InlineData(EmailProviderError.RateLimited)]
    [InlineData(EmailProviderError.Transient)]
    [InlineData(EmailProviderError.NotConfigured)]
    public async Task SyncUserAsync_RetryableProviderError_NeverAdvancesTheCursor(EmailProviderError error)
    {
        // THE rule of the 429/5xx branches: a failed listing is not progress. Persisting the
        // provider's in-memory cursor would skip every message of a partially listed batch.
        var connection = Connection();
        connection.AdvanceCursor("cursor-antes");
        _provider.CursorToEstablish = "cursor-perdido";
        _provider.ListFailure = new EmailProviderException(error, "boom");
        GivenConnections(connection);
        GivenRules(Rule());

        var result = await _service.SyncUserAsync(UserId, CancellationToken.None);

        connection.SyncCursor.Should().Be("cursor-antes");
        connection.SyncEnabled.Should().BeTrue();   // retryable: do NOT disable
        connection.LastError.Should().NotBeNullOrWhiteSpace();
        result.ConnectionErrors.Should().ContainSingle();
        _sent.Should().BeEmpty();
    }

    [Fact]
    public async Task SyncUserAsync_CursorExpiredOnTheFirstListing_RetriesOnceWithANullCursorAndReanchors()
    {
        // Recovery: reset the cursor, re-read the recovery window, then re-anchor at the
        // provider's current position — otherwise every tick would re-read the whole window.
        var connection = Connection();
        connection.AdvanceCursor("cursor-muerto");
        // Only the FIRST listing fails: the recovery retry must succeed, which is the whole
        // point of the single-retry branch.
        _provider.ListFailures.Enqueue(
            new EmailProviderException(EmailProviderError.CursorExpired, "historyId expired"));
        _provider.CurrentCursor = "history-actual";
        GivenConnections(connection);
        GivenRules(Rule(autoCommit: false));

        await _service.SyncUserAsync(UserId, CancellationToken.None);

        _provider.ListCallCount.Should().Be(2);
        // The retry must run with a NULL cursor — that is what makes it a window read.
        _provider.CursorsSeenAtList.Should().Equal("cursor-muerto", null);
        connection.SyncCursor.Should().Be("history-actual");
    }

    [Fact]
    public async Task SyncUserAsync_CursorExpiredAgainOnTheRetry_DropsTheDeadCursorAndRecordsTheError()
    {
        // Both listings fail with CursorExpired. The retry is deliberately not wrapped in a
        // try, so the second failure escapes to the provider-failure handler, whose
        // CursorExpired branch drops the dead cursor so the next tick starts from the
        // recovery window instead of looping on a dead cursor forever.
        var connection = Connection();
        connection.AdvanceCursor("cursor-muerto");
        _provider.ListFailures.Enqueue(
            new EmailProviderException(EmailProviderError.CursorExpired, "expired"));
        _provider.ListFailures.Enqueue(
            new EmailProviderException(EmailProviderError.CursorExpired, "expired"));
        GivenConnections(connection);
        GivenRules(Rule());

        var result = await _service.SyncUserAsync(UserId, CancellationToken.None);

        _provider.ListCallCount.Should().Be(2);   // exactly one retry, not an unbounded loop
        // The retry really ran with a NULL cursor — that is what makes it a window read.
        _provider.CursorsSeenAtList.Should().Equal("cursor-muerto", null);
        connection.SyncCursor.Should().BeNull();
        connection.LastError.Should().Contain("expired");
        connection.SyncEnabled.Should().BeTrue();     // CursorExpired does NOT disable
        result.ConnectionErrors.Should().ContainSingle();
        await _connections.Received(1).UpdateAsync(connection, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncUserAsync_CursorExpiredAndTheProviderCannotReanchor_LeavesTheCursorNull()
    {
        // Best-effort re-anchor: a null cursor is safe (dedupe still works) but wasteful.
        var connection = Connection();
        connection.AdvanceCursor("cursor-muerto");
        _provider.ListFailure =
            new EmailProviderException(EmailProviderError.CursorExpired, "expired");
        _provider.CurrentCursor = null;
        GivenConnections(connection);
        GivenRules(Rule());

        await _service.SyncUserAsync(UserId, CancellationToken.None);

        connection.SyncCursor.Should().BeNull();
    }

    // ── Isolation between messages and between connections ────────────────────────────
    [Fact]
    public async Task SyncUserAsync_ABatchOfMessages_IsStagedOneRowPerMessage()
    {
        var connection = Connection();
        _provider.Messages.Add(Message("msg-1"));
        _provider.Messages.Add(Message("msg-2"));
        _provider.Messages.Add(Message("msg-3"));
        GivenConnections(connection);
        GivenRules(Rule(autoCommit: false));

        var result = await _service.SyncUserAsync(UserId, CancellationToken.None);

        result.Staged.Should().Be(3);
        result.Failed.Should().Be(0);
        await _pending.Received(3)
            .TryAddAsync(Arg.Any<PendingEmailTransaction>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncUserAsync_AnExtractionTheDomainRejects_IsCountedAsFailedAndTheBatchContinues()
    {
        // The adapter can return fields PendingEmailTransaction.Create refuses; that must be
        // counted, never fatal. A zero amount can never become a staging row.
        var connection = Connection();
        _provider.Messages.Add(Message("msg-1", amount: "0,00"));
        _provider.Messages.Add(Message("msg-2"));
        GivenConnections(connection);
        GivenRules(Rule(autoCommit: false));

        var result = await _service.SyncUserAsync(UserId, CancellationToken.None);

        result.Failed.Should().Be(1);
        result.Staged.Should().Be(1);   // the good message still landed
        result.MessagesFetched.Should().Be(2);
    }

    [Fact]
    public async Task SyncUserAsync_AMessageWithNoAmountAtAll_IsCountedAsSkipped()
    {
        // No amount pattern can match, so the adapter reports no match and nothing is
        // persisted. An amount is NEVER invented.
        var connection = Connection();
        _provider.Messages.Add(EmailMessage.Create(
            "msg-1", null, "banco@ejemplo.es", "Newsletter", "devoid de importe alguno",
            new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero)));
        GivenConnections(connection);
        GivenRules(Rule());

        var result = await _service.SyncUserAsync(UserId, CancellationToken.None);

        result.Skipped.Should().Be(1);
        await _pending.DidNotReceive()
            .TryAddAsync(Arg.Any<PendingEmailTransaction>(), Arg.Any<CancellationToken>());
        _sent.Should().BeEmpty();
    }

    [Fact]
    public async Task SyncAllAsync_OneConnectionBlowsUp_StillVisitsTheNextOne()
    {
        var broken = Connection();
        var healthy = EmailConnection.Create(
            Guid.NewGuid(), EmailProvider.Gmail, "otro@banco.es", "v1:bm9uY2U=:Y2lwaGVy:dGFn");
        _connections.GetEnabledAsync(Arg.Any<CancellationToken>()).Returns([broken, healthy]);
        _rules.GetByUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns([Rule(autoCommit: false)]);
        // The resolver throws for every connection; the loop must still visit them all.
        _resolver.Resolve(EmailProvider.Gmail)
            .Returns(_ => throw new InvalidOperationException("provider roto"));

        var result = await _service.SyncAllAsync(CancellationToken.None);

        // Nothing was processed, but the loop survived and reported BOTH failures instead of
        // aborting on the first one.
        result.ConnectionsProcessed.Should().Be(2);
        result.ConnectionErrors.Should().HaveCount(2);
    }

    [Fact]
    public async Task SyncAllAsync_PushesTheConnectionOwnerBeforeProcessingIt()
    {
        // CreateTransactionCommand resolves its owner through ICurrentUserProvider, which
        // outside an HTTP request is this pushed scope. Without it GetUserId() throws.
        var connection = Connection();
        _connections.GetEnabledAsync(Arg.Any<CancellationToken>()).Returns([connection]);
        _rules.GetByUserAsync(UserId, Arg.Any<CancellationToken>()).Returns([Rule(autoCommit: false)]);

        await _service.SyncAllAsync(CancellationToken.None);

        _accessor.Received(1).Push(UserId);
    }

    [Fact]
    public async Task SyncUserAsync_CancelledToken_Throws()
    {
        var connection = Connection();
        _provider.Messages.Add(Message("msg-1"));
        GivenConnections(connection);
        GivenRules(Rule());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => _service.SyncUserAsync(UserId, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
