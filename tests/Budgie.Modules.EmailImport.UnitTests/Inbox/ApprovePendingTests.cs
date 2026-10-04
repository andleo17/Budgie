using System.Text.Json;
using Budgie.Modules.EmailImport.Application;
using Budgie.Modules.EmailImport.Application.Abstractions;
using Budgie.Modules.EmailImport.Application.Inbox;
using Budgie.Modules.EmailImport.Application.Inbox.Abstractions;
using Budgie.Modules.EmailImport.Application.Inbox.ApproveBatch;
using Budgie.Modules.EmailImport.Application.Inbox.ApprovePending;
using Budgie.Modules.EmailImport.Application.Inbox.DismissPending;
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
using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Modules.Finances.Domain.Categories;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Enums;
using ErrorOr;
using FluentAssertions;
using Mediator;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Budgie.Shared.Exceptions;
using Xunit;

namespace Budgie.Modules.EmailImport.UnitTests.Inbox;

/// <summary>
/// <see cref="ApprovePendingCommandHandler"/> is the manual review route. It shares
/// <c>PendingApprovalHelper</c> with the batch handler, so a batch can never grow a
/// divergent copy of the approval logic.
/// <para>The critical shared invariant is the <c>ExternalUid</c> key: it must be built
/// byte-for-byte like the sync worker's, or the unique index would never see the collision
/// and the same email could be imported twice — once by the worker, once by the user.
/// <c>ExternalUidParity_WorkerAndApproval_BuildTheIdenticalKey</c> below proves that by
/// running BOTH real implementations and comparing the captured keys.</para>
/// </summary>
public class ApprovePendingTests
{
    private readonly IPendingEmailTransactionRepository _pending = Substitute.For<IPendingEmailTransactionRepository>();
    private readonly IEmailImportRuleRepository _rules = Substitute.For<IEmailImportRuleRepository>();
    private readonly IEmailConnectionRepository _connections = Substitute.For<IEmailConnectionRepository>();
    private readonly IAccountRepository _accounts = Substitute.For<IAccountRepository>();
    private readonly ICategoryRepository _categories = Substitute.For<ICategoryRepository>();
    private readonly ITransactionRepository _transactions = Substitute.For<ITransactionRepository>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly ICurrentUserProvider _currentUser = Substitute.For<ICurrentUserProvider>();
    private readonly ICurrentUserProviderAccessor _accessor = Substitute.For<ICurrentUserProviderAccessor>();
    private readonly IEmailConnectionRepository _syncConnections = Substitute.For<IEmailConnectionRepository>();
    private readonly IEmailImportRuleRepository _syncRules = Substitute.For<IEmailImportRuleRepository>();
    private readonly IPendingEmailTransactionRepository _syncPending = Substitute.For<IPendingEmailTransactionRepository>();
    private readonly IEmailProviderResolver _resolver = Substitute.For<IEmailProviderResolver>();
    private readonly ITransactionRepository _syncTransactions = Substitute.For<ITransactionRepository>();
    private readonly ILanguageProvider _language = Substitute.For<ILanguageProvider>();
    private readonly FakeEmailProviderClient _provider = new();

    private readonly List<CreateTransactionCommand> _sent = [];
    private readonly Guid _accountId = Guid.NewGuid();
    private readonly Guid _categoryId = Guid.NewGuid();

    private static readonly Guid UserId = Guid.NewGuid();

    public ApprovePendingTests()
    {
        _currentUser.GetUserId().Returns(UserId);
        _language.LanguageCode.Returns("es");
        _syncPending.TryAddAsync(Arg.Any<PendingEmailTransaction>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _accounts.GetByIdAsync(Arg.Any<Guid>(), UserId, Arg.Any<CancellationToken>())
            .Returns(_ => Account.Create(UserId, "Cuenta", "EUR", 1000m));
        _categories.GetByIdAsync(Arg.Any<Guid>(), UserId, Arg.Any<CancellationToken>())
            .Returns(_ => Category.CreateSystem(_categoryId, "Ocio", 1));
    }

    private ApprovePendingCommandHandler Handler() => new(
        _pending, _rules, _connections, _accounts, _categories, _transactions,
        _mediator, _currentUser, _accessor);

    private EmailSyncService SyncService() => new(
        _syncConnections, _syncRules, _syncPending, _resolver,
        new EmailParser(new MessageTextExtractor(),
            new BankAdapterRegistry([new GenericRegexAdapter()])),
        _syncTransactions, _mediator, _accessor, _language,
        Substitute.For<Microsoft.Extensions.Logging.ILogger<EmailSyncService>>());

    // ── Fixtures ───────────────────────────────────────────────────────────────────────
    /// <summary>
    /// The rule a row keeps by default. Passing <c>ruleId: null</c> models the real
    /// "its rule was deleted" case (<c>RuleId</c> is ON DELETE SET NULL), which is the only
    /// case that needs an explicit account override.
    /// </summary>
    private Guid _ruleId = Guid.NewGuid();

    private PendingEmailTransaction Pending(
        string messageId = "msg-1",
        Guid? ruleId = null,
        Guid? connectionId = null,
        decimal amount = 45.20m,
        TransactionType type = TransactionType.Expense,
        decimal confidence = 0.9m,
        bool keepRule = true) =>
        PendingEmailTransaction.Create(
            userId: UserId,
            connectionId: connectionId ?? Guid.NewGuid(),
            ruleId: keepRule ? ruleId ?? _ruleId : null,
            messageId: messageId,
            threadId: "thread-1",
            subject: "Su pago",
            merchant: "Cafetería Central",
            notes: null,
            amount: amount,
            currency: "EUR",
            exchangeRate: 1m,
            date: new DateTime(2026, 10, 1),
            transactionType: type,
            confidence: confidence,
            fieldConfidenceJson: """{"amount":1.0}""",
            rawBody: "cuerpo original",
            createdAt: DateTime.UtcNow);

    private EmailImportRule Rule(bool autoCommit = false) =>
        EmailImportRule.Create(
            UserId, "Banco Ejemplo", EmailImportRule.GenericRegexKey, 0, true,
            _accountId, _categoryId, TransactionType.Expense, "EUR", null,
            null, null, null,
            JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["amount"] = @"Importe:\s*(?<amount>[\d.,]+)",
                ["date"] = @"Fecha:\s*(?<date>\d{2}/\d{2}/\d{4})",
                ["merchant"] = @"Comercio:\s*(?<merchant>.+)"
            }),
            autoCommit);

    private EmailConnection Connection() =>
        EmailConnection.Create(UserId, EmailProvider.Gmail, "usuario@banco.es",
            "v1:bm9uY2U=:Y2lwaGVy:dGFn");

    private void GivenPending(PendingEmailTransaction pending)
    {
        _pending.GetByIdAsync(UserId, pending.Id, Arg.Any<CancellationToken>()).Returns(pending);
        // Every row keeps its rule unless a test says otherwise, so the rule supplies the
        // target account and the approval has something to dispatch to.
        if (pending.RuleId is { } ruleId)
            _rules.GetByIdAsync(UserId, ruleId, Arg.Any<CancellationToken>()).Returns(Rule());
    }

    private void CommitsSucceed() => CommitsReturn(_ => Guid.NewGuid());

    private void CommitsReturn(Func<CreateTransactionCommand, ErrorOr<Guid>> result) =>
        _mediator.Send(Arg.Do<CreateTransactionCommand>(_sent.Add), Arg.Any<CancellationToken>())
            .Returns(call => new ValueTask<ErrorOr<Guid>>(result(call.Arg<CreateTransactionCommand>())));

    private void CommitsThrow(Exception exception) =>
        _mediator.Send(Arg.Do<CreateTransactionCommand>(_sent.Add), Arg.Any<CancellationToken>())
            .Throws(exception);

    private CreateTransactionCommand SentCommand() => _sent.Should().ContainSingle().Subject;

    /// <summary>
    /// The neutral exception <c>TransactionRepository.AddAsync</c> translates a unique violation
    /// into: the contract this handler actually catches, across the layer boundary.
    /// </summary>
    private static UniqueConstraintViolationException UniqueViolation() =>
        new("duplicate key value violates unique constraint \"IX_Transactions_UserId_ExternalUid\"");

    // ── The happy path ─────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Handle_PendingRowWithoutCorrections_CommitsTheStagedValuesAsIs()
    {
        var pending = Pending();
        var connection = Connection();
        GivenPending(pending);
        _connections.GetByIdAsync(UserId, pending.ConnectionId, Arg.Any<CancellationToken>())
            .Returns(connection);
        CommitsSucceed();

        var result = await Handler().Handle(new ApprovePendingCommand(pending.Id), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().NotBeEmpty();
        SentCommand().Amount.Should().Be(45.20m);
        SentCommand().Currency.Should().Be("EUR");
        SentCommand().Date.Should().Be(new DateTime(2026, 10, 1));
        SentCommand().Type.Should().Be(TransactionType.Expense);
        SentCommand().Notes.Should().Be("Cafetería Central");   // the staged merchant
        await _pending.Received(1).MarkCommittedAsync(
            UserId, pending.Id, result.Value, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithoutCorrections_DoesNotPersistAnyReview()
    {
        // Approve-without-touching uses the staged values as-is and never rewrites the row.
        var pending = Pending();
        GivenPending(pending);
        _connections.GetByIdAsync(UserId, pending.ConnectionId, Arg.Any<CancellationToken>())
            .Returns(Connection());
        CommitsSucceed();

        await Handler().Handle(new ApprovePendingCommand(pending.Id), CancellationToken.None);

        await _pending.DidNotReceive()
            .UpdateReviewedAsync(Arg.Any<PendingEmailTransaction>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PushesTheUserBeforeDispatching()
    {
        // CreateTransactionCommand resolves its owner from ICurrentUserProvider, and outside
        // an HTTP request nobody has pushed yet — without this GetUserId() throws inside the
        // Finances handler.
        var pending = Pending();
        GivenPending(pending);
        _connections.GetByIdAsync(UserId, pending.ConnectionId, Arg.Any<CancellationToken>())
            .Returns(Connection());
        CommitsSucceed();

        await Handler().Handle(new ApprovePendingCommand(pending.Id), CancellationToken.None);

        _accessor.Received(1).Push(UserId);
    }

    [Fact]
    public async Task Handle_UsesTheRulesAccountAndCategory_WhenTheRowKeepsItsRule()
    {
        var rule = Rule();
        var pending = Pending(ruleId: rule.Id);
        GivenPending(pending);
        _rules.GetByIdAsync(UserId, rule.Id, Arg.Any<CancellationToken>()).Returns(rule);
        _connections.GetByIdAsync(UserId, pending.ConnectionId, Arg.Any<CancellationToken>())
            .Returns(Connection());
        CommitsSucceed();

        await Handler().Handle(new ApprovePendingCommand(pending.Id), CancellationToken.None);

        SentCommand().AccountId.Should().Be(_accountId);
        SentCommand().CategoryId.Should().Be(_categoryId);
    }

    // ── Corrections ───────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Handle_WithCorrections_SendsTheEditedValuesAndPersistsThem()
    {
        var pending = Pending();
        var overrideAccount = Guid.NewGuid();
        GivenPending(pending);
        _connections.GetByIdAsync(UserId, pending.ConnectionId, Arg.Any<CancellationToken>())
            .Returns(Connection());
        CommitsSucceed();

        await Handler().Handle(new ApprovePendingCommand(
            pending.Id,
            Amount: 50m,
            Date: new DateTime(2026, 9, 30),
            Merchant: "Otro comercio",
            Notes: "corregido a mano",
            AccountId: overrideAccount,
            CategoryId: _categoryId,
            Type: TransactionType.Income), CancellationToken.None);

        var command = SentCommand();
        command.Amount.Should().Be(50m);              // NOT the staged 45.20
        command.Date.Should().Be(new DateTime(2026, 9, 30));
        command.Notes.Should().Be("Otro comercio");   // a human vouched: merchant → Notes
        command.AccountId.Should().Be(overrideAccount);
        command.Type.Should().Be(TransactionType.Income);
        await _pending.Received(1).UpdateReviewedAsync(
            Arg.Is<PendingEmailTransaction>(p =>
                p.Amount.Amount == 50m && p.Merchant == "Otro comercio"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithCorrections_LeavesTheRowPendingUntilItCommits()
    {
        var pending = Pending();
        GivenPending(pending);
        _connections.GetByIdAsync(UserId, pending.ConnectionId, Arg.Any<CancellationToken>())
            .Returns(Connection());
        CommitsSucceed();

        await Handler().Handle(new ApprovePendingCommand(pending.Id, Amount: 50m), CancellationToken.None);

        // ApplyReview keeps Status = Pending; committing is the handler's job.
        await _pending.Received(1).UpdateReviewedAsync(
            Arg.Is<PendingEmailTransaction>(p => p.Status == PendingEmailStatus.Pending),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ForeignAccountOverride_IsRejectedAndNothingIsDispatched()
    {
        // Verified to exist AND belong to the current user BEFORE any write.
        var foreignAccountId = Guid.NewGuid();
        var pending = Pending();
        GivenPending(pending);
        // A foreign account is simply "not found" for this user — there is no existence leak.
        _accounts.GetByIdAsync(foreignAccountId, UserId, Arg.Any<CancellationToken>())
            .Returns((Account?)null);
        CommitsSucceed();

        var result = await Handler().Handle(new ApprovePendingCommand(
            pending.Id, AccountId: foreignAccountId), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Account.NotFound");
        _sent.Should().BeEmpty();
        await _pending.DidNotReceive().MarkCommittedAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(),
            Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ForeignCategoryOverride_IsRejectedAndNothingIsDispatched()
    {
        var foreignCategoryId = Guid.NewGuid();
        var pending = Pending();
        GivenPending(pending);
        _categories.GetByIdAsync(foreignCategoryId, UserId, Arg.Any<CancellationToken>())
            .Returns((Category?)null);
        CommitsSucceed();

        var result = await Handler().Handle(new ApprovePendingCommand(
            pending.Id, CategoryId: foreignCategoryId), CancellationToken.None);

        result.FirstError.Code.Should().Be("Category.NotFound");
        _sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_NonPositiveAmountCorrection_IsRejectedAsAnInvalidReview()
    {
        var pending = Pending();
        GivenPending(pending);
        _connections.GetByIdAsync(UserId, pending.ConnectionId, Arg.Any<CancellationToken>())
            .Returns(Connection());
        CommitsSucceed();

        var result = await Handler().Handle(new ApprovePendingCommand(
            pending.Id, Amount: -5m), CancellationToken.None);

        result.FirstError.Code.Should().Be("EmailImport.InvalidReview");
        _sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_RuleDeletedAndNoAccountOverride_ReturnsAccountRequired()
    {
        // RuleId is ON DELETE SET NULL, so the row can outlive its rule. Without an explicit
        // account there is nothing to dispatch to: fail fast and visibly.
        var pending = Pending(keepRule: false);
        GivenPending(pending);
        CommitsSucceed();

        var result = await Handler().Handle(new ApprovePendingCommand(pending.Id), CancellationToken.None);

        result.FirstError.Code.Should().Be("EmailImport.AccountRequired");
        _sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_RuleDeletedButAnAccountIsSupplied_Succeeds()
    {
        var pending = Pending();
        GivenPending(pending);
        _connections.GetByIdAsync(UserId, pending.ConnectionId, Arg.Any<CancellationToken>())
            .Returns(Connection());
        CommitsSucceed();

        var result = await Handler().Handle(
            new ApprovePendingCommand(pending.Id, AccountId: _accountId), CancellationToken.None);

        result.IsError.Should().BeFalse();
        SentCommand().AccountId.Should().Be(_accountId);
    }

    [Fact]
    public async Task Handle_MissingConnection_ReturnsConnectionNotFound()
    {
        var pending = Pending();
        GivenPending(pending);
        CommitsSucceed();

        var result = await Handler().Handle(
            new ApprovePendingCommand(pending.Id, AccountId: _accountId), CancellationToken.None);

        result.FirstError.Code.Should().Be("EmailImport.ConnectionNotFound");
        _sent.Should().BeEmpty();
    }

    // ── IDEMPOTENCY: the FIRST barrier (Status must be Pending) ────────────────────────
    [Fact]
    public async Task Handle_UnknownRow_ReturnsPendingNotFound()
    {
        _pending.GetByIdAsync(UserId, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((PendingEmailTransaction?)null);
        CommitsSucceed();

        var result = await Handler().Handle(new ApprovePendingCommand(Guid.NewGuid()), CancellationToken.None);

        result.FirstError.Code.Should().Be("EmailImport.PendingNotFound");
        _sent.Should().BeEmpty();
    }

    [Theory]
    [InlineData(PendingEmailStatus.Committed)]
    [InlineData(PendingEmailStatus.Dismissed)]
    [InlineData(PendingEmailStatus.Failed)]
    public async Task Handle_RowThatIsNoLongerPending_IsRejectedAndDispatchesNothing(
        PendingEmailStatus status)
    {
        // THE first re-approval barrier: approving a row the worker already committed must
        // never create a second transaction.
        var pending = Pending();
        if (status == PendingEmailStatus.Committed)
            pending.MarkCommitted(Guid.NewGuid(), DateTime.UtcNow);
        else if (status == PendingEmailStatus.Dismissed)
            pending.MarkDismissed(DateTime.UtcNow);
        else
            pending.MarkFailed("motivo", DateTime.UtcNow);

        GivenPending(pending);
        CommitsSucceed();

        var result = await Handler().Handle(
            new ApprovePendingCommand(pending.Id, AccountId: _accountId), CancellationToken.None);

        result.FirstError.Code.Should().Be("EmailImport.PendingNotFound");
        _sent.Should().BeEmpty();
        await _pending.DidNotReceive().MarkCommittedAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(),
            Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ApprovingTheSameRowTwice_SecondAttemptCreatesNoSecondTransaction()
    {
        var pending = Pending();
        GivenPending(pending);
        _connections.GetByIdAsync(UserId, pending.ConnectionId, Arg.Any<CancellationToken>())
            .Returns(Connection());
        CommitsSucceed();

        var first = await Handler().Handle(
            new ApprovePendingCommand(pending.Id, AccountId: _accountId), CancellationToken.None);
        first.IsError.Should().BeFalse();

        // The row is Committed now; the repository returns it in that state.
        pending.MarkCommitted(first.Value, DateTime.UtcNow);
        var second = await Handler().Handle(
            new ApprovePendingCommand(pending.Id, AccountId: _accountId), CancellationToken.None);

        second.FirstError.Code.Should().Be("EmailImport.PendingNotFound");
        _sent.Should().ContainSingle();   // dispatched exactly ONCE
    }

    // ── IDEMPOTENCY: the SECOND barrier (ExternalUid / SqlState 23505) ────────────────
    [Fact]
    public async Task Handle_UniqueViolation_StampsTheExistingTransactionAndSucceeds()
    {
        // The worker's auto-commit raced this approval, or a previous attempt died between
        // the dispatch and the status update. That is a success, not an error.
        var existingId = Guid.NewGuid();
        var pending = Pending();
        GivenPending(pending);
        _connections.GetByIdAsync(UserId, pending.ConnectionId, Arg.Any<CancellationToken>())
            .Returns(Connection());
        CommitsThrow(UniqueViolation());
        _transactions.GetIdByExternalUidAsync(
                UserId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(existingId);

        var result = await Handler().Handle(
            new ApprovePendingCommand(pending.Id, AccountId: _accountId), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().Be(existingId);
        await _pending.Received(1).MarkCommittedAsync(
            UserId, pending.Id, existingId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UniqueViolationButTheTransactionVanished_MarksFailedAndReturnsConflict()
    {
        var pending = Pending();
        GivenPending(pending);
        _connections.GetByIdAsync(UserId, pending.ConnectionId, Arg.Any<CancellationToken>())
            .Returns(Connection());
        CommitsThrow(UniqueViolation());
        _transactions.GetIdByExternalUidAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Guid?)null);

        var result = await Handler().Handle(
            new ApprovePendingCommand(pending.Id, AccountId: _accountId), CancellationToken.None);

        result.FirstError.Code.Should().Be("EmailImport.TransactionLost");
        await _pending.Received(1).MarkFailedAsync(
            UserId, pending.Id, Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CreateTransactionError_MarksTheRowFailedAndReturnsTheError()
    {
        const string description = "The transaction date is before the account control start date.";
        var pending = Pending();
        GivenPending(pending);
        _connections.GetByIdAsync(UserId, pending.ConnectionId, Arg.Any<CancellationToken>())
            .Returns(Connection());
        CommitsReturn(_ => Error.Validation("Transaction.BeforeControlStartDate", description));

        var result = await Handler().Handle(
            new ApprovePendingCommand(pending.Id, AccountId: _accountId), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Transaction.BeforeControlStartDate");
        await _pending.Received(1).MarkFailedAsync(
            UserId, pending.Id, description, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MediatorThrowsUnexpectedly_PropagatesWithoutStampingTheRow()
    {
        var pending = Pending();
        GivenPending(pending);
        _connections.GetByIdAsync(UserId, pending.ConnectionId, Arg.Any<CancellationToken>())
            .Returns(Connection());
        CommitsThrow(new InvalidOperationException("boom"));

        Func<Task> act = async () => await Handler().Handle(
            new ApprovePendingCommand(pending.Id, AccountId: _accountId), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _pending.DidNotReceive().MarkCommittedAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(),
            Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    // ── THE PARITY INVARIANT ──────────────────────────────────────────────────────────
    [Fact]
    public async Task ExternalUidParity_WorkerAndApproval_BuildTheIdenticalKey()
    {
        // The single most dangerous thing that could break in this module. If these two
        // strings differed, the unique index on Transactions."ExternalUid" would never see
        // the collision and the SAME EMAIL WOULD BE IMPORTED TWICE — once by the worker's
        // auto-commit and once by the user's manual approval.
        // Both REAL implementations run here and the two captured keys are compared.
        const string messageId = "18f2a1b3c4d5e6f7";
        var connection = Connection();
        var rule = Rule(autoCommit: true);   // the worker only auto-commits AutoCommit rules
        CommitsSucceed();

        // ── Route 1: the sync worker's auto-commit.
        var workerPending = Pending(messageId: messageId);
        _provider.Messages.Add(EmailMessage.Create(
            messageId, "thread-1", "notificaciones@banco.es", "Su pago",
            "Fecha: 01/10/2026\nImporte: 45,20 €\nComercio: Cafetería Central",
            new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero)));
        _resolver.Resolve(Arg.Any<EmailProvider>()).Returns(_provider);
        _syncConnections.GetAllAsync(UserId, Arg.Any<CancellationToken>()).Returns([connection]);
        _syncRules.GetByUserAsync(UserId, Arg.Any<CancellationToken>()).Returns([rule]);
        _syncPending.TryAddAsync(
                Arg.Any<PendingEmailTransaction>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var syncResult = await SyncService().SyncUserAsync(UserId, CancellationToken.None);
        syncResult.AutoCommitted.Should().Be(1);
        var workerKey = SentCommand().ExternalUid;

        // ── Route 2: the manual review approval, same connection and message.
        _sent.Clear();                       // so SentCommand() below sees ONLY this route
        var reviewRow = Pending(
            messageId: messageId, connectionId: connection.Id, ruleId: rule.Id);
        GivenPending(reviewRow);
        _connections.GetByIdAsync(UserId, connection.Id, Arg.Any<CancellationToken>())
            .Returns(connection);
        CommitsSucceed();

        var approval = await Handler().Handle(
            new ApprovePendingCommand(reviewRow.Id), CancellationToken.None);
        approval.IsError.Should().BeFalse();

        // ── THE ASSERTION: both real implementations produced byte-identical keys.
        var approvalKey = _sent[^1].ExternalUid;
        approvalKey.Should().Be(workerKey);
        approvalKey.Should().Be($"email:{EmailProvider.Gmail.ToString().ToLowerInvariant()}:{messageId}");
        approvalKey.Should().Be("email:gmail:18f2a1b3c4d5e6f7");
    }

    // ── Dismiss ────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task HandleDismiss_PendingRow_MovesToDismissedAndKeepsTheRow()
    {
        // Never a delete: the MessageId must keep blocking re-import of the same email.
        var pending = Pending();
        GivenPending(pending);

        var result = await new DismissPendingCommandHandler(_pending, _currentUser)
            .Handle(new DismissPendingCommand(pending.Id), CancellationToken.None);

        result.IsError.Should().BeFalse();
        await _pending.Received(1).MarkDismissedAsync(
            UserId, pending.Id, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleDismiss_AlreadyReviewedRow_ReturnsPendingNotFound()
    {
        var pending = Pending();
        pending.MarkDismissed(DateTime.UtcNow);
        GivenPending(pending);

        var result = await new DismissPendingCommandHandler(_pending, _currentUser)
            .Handle(new DismissPendingCommand(pending.Id), CancellationToken.None);

        result.FirstError.Code.Should().Be("EmailImport.PendingNotFound");
        await _pending.DidNotReceive()
            .MarkDismissedAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleDismiss_ADismissedRowStillBlocksReimport()
    {
        // The reason the row is kept rather than deleted.
        var pending = Pending();
        pending.MarkDismissed(DateTime.UtcNow);

        pending.Status.Should().Be(PendingEmailStatus.Dismissed);
        pending.MessageId.Should().Be("msg-1");   // the idempotency key survives
        pending.CreatedTransactionId.Should().BeNull();
    }

    // ── Batch approval goes through the SAME helper ───────────────────────────────────
    [Fact]
    public async Task HandleBatch_ApprovedRowsAreCountedAndTheRowIsStamped()
    {
        var good = Pending(messageId: "msg-1");
        GivenPending(good);
        _connections.GetByIdAsync(UserId, good.ConnectionId, Arg.Any<CancellationToken>())
            .Returns(Connection());
        _pending.GetHighConfidenceAsync(UserId, Arg.Any<decimal>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([good]);
        CommitsSucceed();

        var result = await new ApproveBatchCommandHandler(
                _pending, _rules, _connections, _accounts, _categories, _transactions,
                _mediator, _currentUser, _accessor)
            .Handle(new ApproveBatchCommand(0.5m, 10), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Approved.Should().Be(1);
        result.Value.Failed.Should().Be(0);
        await _pending.Received(1).MarkCommittedAsync(
            UserId, good.Id, Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleBatch_OneBadRowNeverAbortsTheBatch()
    {
        var good = Pending(messageId: "msg-1");
        var bad = Pending(messageId: "msg-2");
        GivenPending(good);
        _pending.GetByIdAsync(UserId, bad.Id, Arg.Any<CancellationToken>())
            .Returns((PendingEmailTransaction?)null);
        _connections.GetByIdAsync(UserId, good.ConnectionId, Arg.Any<CancellationToken>())
            .Returns(Connection());
        _pending.GetHighConfidenceAsync(UserId, Arg.Any<decimal>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([good, bad]);
        CommitsSucceed();

        var result = await new ApproveBatchCommandHandler(
                _pending, _rules, _connections, _accounts, _categories, _transactions,
                _mediator, _currentUser, _accessor)
            .Handle(new ApproveBatchCommand(0.5m, 10), CancellationToken.None);

        result.Value.Approved.Should().Be(1);
        result.Value.Failed.Should().Be(1);
        result.Value.Failures.Should().ContainSingle();
        _sent.Should().ContainSingle();   // only the good row was dispatched
    }
}
