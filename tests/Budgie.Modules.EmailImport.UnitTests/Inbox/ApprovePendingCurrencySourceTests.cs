using Budgie.Modules.EmailImport.Application;
using Budgie.Modules.EmailImport.Application.Inbox;
using Budgie.Modules.EmailImport.Application.Inbox.Abstractions;
using Budgie.Modules.EmailImport.Application.Inbox.ApprovePending;
using Budgie.Modules.EmailImport.Domain.Connections;
using Budgie.Modules.EmailImport.Domain.Rules;
using Budgie.Modules.EmailImport.Domain.Staging;
using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Application.Transactions.CreateTransaction;
using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Shared.Abstractions;
using ErrorOr;
using FluentAssertions;
using Mediator;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.EmailImport.UnitTests.Inbox;

/// <summary>
/// The approval dispatch used to take the currency and the exchange rate from the RULE, so
/// editing a rule between staging and approval dispatched an amount the user had vetted as
/// EUR carrying the rule's new USD. The reviewed row is the artifact the user actually
/// checked, so it must be the source of both.
/// </summary>
public class ApprovePendingCurrencySourceTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly IPendingEmailTransactionRepository _pending = Substitute.For<IPendingEmailTransactionRepository>();
    private readonly IEmailImportRuleRepository _rules = Substitute.For<IEmailImportRuleRepository>();
    private readonly IEmailConnectionRepository _connections = Substitute.For<IEmailConnectionRepository>();
    private readonly IAccountRepository _accounts = Substitute.For<IAccountRepository>();
    private readonly ICategoryRepository _categories = Substitute.For<ICategoryRepository>();
    private readonly ITransactionRepository _transactions = Substitute.For<ITransactionRepository>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly ICurrentUserProvider _currentUser = Substitute.For<ICurrentUserProvider>();
    private readonly ICurrentUserProviderAccessor _accessor = Substitute.For<ICurrentUserProviderAccessor>();

    public ApprovePendingCurrencySourceTests()
    {
        _currentUser.GetUserId().Returns(UserId);
        _accounts.GetByIdAsync(Arg.Any<Guid>(), UserId, Arg.Any<CancellationToken>())
            .Returns(ci => Account.Create(UserId, "Cuenta", "EUR", 1000m));
        _mediator.Send(Arg.Any<CreateTransactionCommand>(), Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<ErrorOr<Guid>>(Guid.NewGuid()));
    }

    private ApprovePendingCommandHandler Handler() => new(
        _pending, _rules, _connections, _accounts, _categories, _transactions,
        _mediator, _currentUser, _accessor);

    private Guid ArrangeRowAndRule(out Guid pendingId)
    {
        var accountId = Guid.NewGuid();
        var ruleId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();

        var connection = EmailConnection.Create(UserId, Shared.Enums.EmailProvider.Gmail, "u@gmail.com", "v1:c");

        // The rule now says USD / 1.25 — it was edited after the row was staged.
        var rule = EmailImportRule.Create(
            userId: UserId, name: "Regla", adapterKey: EmailImportRule.GenericRegexKey,
            priority: 100, isEnabled: true, accountId: accountId, categoryId: null,
            transactionType: Shared.Enums.TransactionType.Expense,
            currency: "USD", exchangeRate: 1.25m,
            fromAddress: null, subjectContains: null, bodyRegex: null,
            fieldPatternsJson: """{"amount":"([0-9.,]+)"}""", autoCommit: true);

        var pending = PendingEmailTransaction.Create(
            userId: UserId, connectionId: connectionId, ruleId: ruleId,
            messageId: "msg-1", threadId: null, subject: "Aviso",
            merchant: "ACME", notes: null,
            amount: 45.20m, currency: "EUR", exchangeRate: 1m,
            date: new DateTime(2026, 10, 2),
            transactionType: Shared.Enums.TransactionType.Expense,
            confidence: 0.9m, fieldConfidenceJson: null, rawBody: null,
            createdAt: new DateTime(2026, 10, 2, 10, 0, 0, DateTimeKind.Unspecified));

        pendingId = pending.Id;
        _pending.GetByIdAsync(UserId, pending.Id, Arg.Any<CancellationToken>()).Returns(pending);
        _rules.GetByIdAsync(UserId, ruleId, Arg.Any<CancellationToken>()).Returns(rule);
        _connections.GetByIdAsync(UserId, connectionId, Arg.Any<CancellationToken>()).Returns(connection);
        _accounts.GetByIdAsync(accountId, UserId, Arg.Any<CancellationToken>())
            .Returns(Account.Create(UserId, "Cuenta", "EUR", 1000m));

        return ruleId;
    }

    [Fact]
    public async Task Approve_UsesTheReviewedRowsCurrencyNotTheRules()
    {
        ArrangeRowAndRule(out var pendingId);

        var result = await Handler().Handle(
            new ApprovePendingCommand(pendingId, null, null, null, null, null, null, null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();

        await _mediator.Received(1).Send(
            Arg.Is<CreateTransactionCommand>(c =>
                c.Currency == "EUR" &&
                c.ExchangeRate == 1m &&
                c.Amount == 45.20m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Approve_DoesNotWriteTheRulesCurrencyBackIntoTheReviewedRow()
    {
        ArrangeRowAndRule(out var pendingId);

        await Handler().Handle(
            new ApprovePendingCommand(pendingId, null, null, null, null, null, null, null),
            CancellationToken.None);

        // With no corrections the row must not be rewritten at all; with corrections, the
        // currency carried into ApplyReview must still be the staged one, never the rule's.
        await _pending.DidNotReceive().UpdateReviewedAsync(
            Arg.Any<PendingEmailTransaction>(), Arg.Any<CancellationToken>());
    }
}
