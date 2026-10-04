using Budgie.Modules.EmailImport.Application;
using Budgie.Modules.EmailImport.Application.Rules;
using Budgie.Modules.EmailImport.Application.Rules.GetRules;
using Budgie.Modules.EmailImport.Domain.Rules;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Enums;
using ErrorOr;
using FluentAssertions;
using Mediator;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.EmailImport.UnitTests.Rules;

/// <summary>
/// Slice 10 (§3.4): the domain stores <c>Currency</c> as a <c>CurrencyCode</c> value object,
/// but the rules API read model stays a plain <c>string</c> — the canonical code, never the
/// raw input. These tests pin that mapping: without them, an adapter or refactor could start
/// leaking the value object (or a non-canonical string) to API clients unnoticed.
/// </summary>
public class GetRulesQueryHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();

    private const string ValidPatterns = """{"amount":"Importe:\\s*(?<amount>[\\d.,]+)"}""";

    [Fact]
    public async Task Handle_ReturnsTheCanonicalCurrencyCodeAsString()
    {
        // Rule stored with a lowercase currency: the domain normalizes to "USD" at write
        // time, and the read model must emit that canonical code.
        var rule = EmailImportRule.Create(
            UserId, "Banco", EmailImportRule.GenericRegexKey, 0, true,
            AccountId, null, TransactionType.Expense, "usd", null,
            null, null, null, ValidPatterns, false);

        var (handler, repository) = BuildHandler([rule]);

        var result = await handler.Handle(new GetRulesQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().ContainSingle();
        result.Value[0].Currency.Should().Be("USD"); // string, canonical — not the VO
        result.Value[0].Currency.Should().Be(rule.Currency.Code);
    }

    [Fact]
    public async Task Handle_OrdersByPriorityAscending_SoTheUIShowsEvaluationOrder()
    {
        // GetRulesQueryHandler orders by Priority; the UI lists rules in the same order the
        // parser will evaluate them, so this ordering is part of the contract, not cosmetics.
        var low = MakeRule(priority: 10);
        var high = MakeRule(priority: 1);

        var (handler, _) = BuildHandler([low, high]);

        var result = await handler.Handle(new GetRulesQuery(), CancellationToken.None);

        result.Value.Select(r => r.Priority).Should().Equal(1, 10);
    }

    private static EmailImportRule MakeRule(int priority) =>
        EmailImportRule.Create(
            UserId, $"Regla {priority}", EmailImportRule.GenericRegexKey, priority, true,
            AccountId, null, TransactionType.Expense, "EUR", null,
            null, null, null, ValidPatterns, false);

    private static (GetRulesQueryHandler Handler, IEmailImportRuleRepository Repository) BuildHandler(
        List<EmailImportRule> rules)
    {
        var repository = Substitute.For<IEmailImportRuleRepository>();
        repository.GetByUserAsync(UserId, Arg.Any<CancellationToken>()).Returns(rules);

        var currentUser = Substitute.For<ICurrentUserProvider>();
        currentUser.GetUserId().Returns(UserId);

        return (new GetRulesQueryHandler(repository, currentUser), repository);
    }
}
