using System.Text.Json;
using Budgie.Modules.EmailImport.Application.Abstractions;
using Budgie.Modules.EmailImport.Application.Parsing;
using Budgie.Modules.EmailImport.Application.Parsing.Adapters;
using Budgie.Modules.EmailImport.Domain.Connections;
using Budgie.Modules.EmailImport.Domain.Parsing;
using Budgie.Modules.EmailImport.Domain.Rules;
using Budgie.Shared.Enums;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.EmailImport.UnitTests.Parsing;

/// <summary>
/// Slice 3 (§2.3, option (c)): a currency symbol that CONTRADICTS the rule's declared
/// currency is a verdict, not a penalty — <see cref="ParseOutcome.RuleRejected"/> — so it can
/// never auto-commit. And a rejected rule is NOT terminal: the parser keeps trying the
/// remaining rules, because a later rule may be the correct owner of the message.
/// </summary>
/// <remarks>
/// These tests exist because the behaviour they pin has two distinct ways of being wrong, and
/// the naive reading of option (c) produced the second one.
/// </remarks>
public class EmailParserCurrencyRejectionTests
{
    private static readonly Guid AccountId = Guid.NewGuid();

    private readonly IMessageTextExtractor _extractor = Substitute.For<IMessageTextExtractor>();
    private readonly BankAdapterRegistry _registry;

    public EmailParserCurrencyRejectionTests() => _registry = new BankAdapterRegistry([new GenericRegexAdapter()]);

    private EmailParser Parser() => new(_extractor, _registry);

    /// <summary>
    /// A rule whose amount pattern embeds a <c>symbol</c> group, so the adapter can capture the
    /// currency marker from the message itself.
    /// </summary>
    private static EmailImportRule Rule(string currency, int priority, string subjectContains = "Su pago") =>
        EmailImportRule.Create(
            Guid.NewGuid(), $"regla {currency}", EmailImportRule.GenericRegexKey, priority, true,
            AccountId, null, TransactionType.Expense, currency, null,
            null, subjectContains, null,
            FieldPatterns(@"Importe:\s*(?<amount>[\d.,]+)\s*(?<symbol>[^\s]+)"),
            autoCommit: false);

    private static string FieldPatterns(string amount)
    {
        var map = new Dictionary<string, string?> { ["amount"] = amount };
        return JsonSerializer.Serialize(map);
    }

    private static EmailMessage Message(string body = "raw body") =>
        EmailMessage.Create("m1", "t1", "notificaciones@banco.es", "Su pago", body,
            new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero));

    private void ExtractorReturns(string plainText) =>
        _extractor.ExtractPlainText(Arg.Any<EmailMessage>()).Returns(plainText);

    // ── The contradiction itself ───────────────────────────────────────────────────────

    // The parser's PUBLIC outcome is always terminal: RuleRejected is a signal the parser
    // consumes while looping over rules, never something it hands back. These tests therefore
    // assert the terminal form — a NoMatch carrying the contradiction reason — and the
    // adapter-level flag is pinned separately in GenericRegexAdapterCurrencyRejectionTests.
    [Fact]
    public async Task ParseAsync_ContradictingSymbol_WithNoOtherRule_IsNoMatchWithTheContradictionReason()
    {
        // A $-denominated message against a EUR rule. Before this slice the currency field scored
        // 0.0, which cost only 0.10 of 1.00: 0.90, above the 0.85 auto-commit threshold, so the
        // transaction was created labelled EUR with nobody looking at it.
        ExtractorReturns("Importe: 45,20 $");

        var outcome = await Parser().ParseAsync(Message(), [Rule("EUR", 1)], "es", CancellationToken.None);

    outcome.Matched.Should().BeFalse("a contradicting currency must never auto-commit");
    outcome.Value.Should().BeNull();
    outcome.SkipReason.Should().Be(
        GenericRegexAdapter.CurrencyContradictionReason,
        "the email is skipped, but with a reason the user can act on");
}

    // ── THE REGRESSION TEST for the naive reading of option (c) ────────────────────────

    [Fact]
    public async Task ParseAsync_RejectedByFirstRule_StillTriesTheNextRule()
    {
        // The EUR rule (priority 1) rejects the dollar message. The USD rule (priority 2) can
        // own it. A plain NoMatch from the first rule would discard the message HERE and the USD
        // rule would never be evaluated — trading a wrong-currency auto-commit for a silently
        // dropped email. This is the test that fails if option (c) is implemented naively.
        ExtractorReturns("Importe: 45,20 $");

        var outcome = await Parser().ParseAsync(
            Message(),
            [Rule("EUR", 1), Rule("USD", 2)],
            "es",
            CancellationToken.None);

        outcome.Matched.Should().BeTrue("a later rule can own a message an earlier rule rejected");
        outcome.Value!.Currency.Should().Be("USD");
        outcome.MatchedRule!.Currency.Code.Should().Be("USD");
    }

    [Fact]
    public async Task ParseAsync_EveryRuleRejects_IsNoMatchWithTheContradictionReason()
    {
        // No rule can own it: the email is skipped, but with a reason the user can act on
        // instead of the opaque "no-rule-matched".
        ExtractorReturns("Importe: 45,20 $");

        var outcome = await Parser().ParseAsync(
            Message(),
            [Rule("EUR", 1), Rule("GBP", 2)],
            "es",
            CancellationToken.None);

        outcome.Matched.Should().BeFalse();
        outcome.RuleRejected.Should().BeFalse("the parser ran out of rules: the outcome is terminal");
        outcome.SkipReason.Should().Be(GenericRegexAdapter.CurrencyContradictionReason);
    }

    [Fact]
    public async Task ParseAsync_NoRuleFiltersMatch_KeepsTheGenericReason()
    {
        // A rejection reason must not leak into a message that simply matched nothing.
        ExtractorReturns("Importe: 45,20 $");

        var outcome = await Parser().ParseAsync(
            Message(),
            [Rule("EUR", 1, subjectContains: "Otro asunto")],
            "es",
            CancellationToken.None);

        outcome.Matched.Should().BeFalse();
        outcome.SkipReason.Should().Be("no-rule-matched");
    }

    // ── Unknown symbol is NOT a contradiction (conservative on purpose) ────────────────

    [Fact]
    public async Task ParseAsync_UnknownSymbol_IsNotRejected_ItGoesToReviewWithZeroCurrency()
    {
        // "CHF" is not in the symbol table. We do NOT know the currency is wrong; we only know we
        // cannot identify it. Rejecting here would silently stop importing for a user with a CHF
        // account, over a gap in our own table.
        ExtractorReturns("Importe: 45,20 CHF");

        var outcome = await Parser().ParseAsync(Message(), [Rule("EUR", 1)], "es", CancellationToken.None);

        outcome.RuleRejected.Should().BeFalse("an unmappable symbol is our ignorance, not a contradiction");
        outcome.Matched.Should().BeTrue();
        outcome.Value!.FieldConfidence[ConfidenceScorer.CurrencyField].Should().Be(0m);
        outcome.Value.Confidence.Should().BeLessThan(
            AutoCommitThreshold,
            "a zero currency score must keep the message below the auto-commit threshold");
    }

    [Fact]
    public async Task ParseAsync_NoSymbolCaptured_KeepsCurrencyAtOne()
    {
        // No symbol in the pattern at all: the rule declares the currency explicitly.
        ExtractorReturns("Importe: 45,20");

        var eurRule = EmailImportRule.Create(
            Guid.NewGuid(), "sin símbolo", EmailImportRule.GenericRegexKey, 1, true,
            AccountId, null, TransactionType.Expense, "EUR", null,
            null, "Su pago", null,
            FieldPatterns(@"Importe:\s*(?<amount>[\d.,]+)"),
            autoCommit: false);

        var outcome = await Parser().ParseAsync(Message(), [eurRule], "es", CancellationToken.None);

        outcome.Matched.Should().BeTrue();
        outcome.Value!.FieldConfidence[ConfidenceScorer.CurrencyField].Should().Be(1m);
    }

    // ── §6.8 deterministic tie-break ──────────────────────────────────────────────────

    [Fact]
    public async Task ParseAsync_SamePriority_TieBreaksOnRuleIdDeterministically()
    {
        // Two rules, identical priority. Which one wins used to depend on the order the database
        // returned them — invisible in the code, and only observable as "the same email imports
        // differently from one day to the next". Now the lower Id wins, every time.
        ExtractorReturns("Importe: 45,20 €");

        var first = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var second = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var withLowIdFirst = RuleWithId(first, "EUR");
        var withHighIdFirst = RuleWithId(second, "EUR");
        var lowId = RuleWithId(first, "EUR");

        var run1 = await Parser().ParseAsync(Message(), [withLowIdFirst, withHighIdFirst], "es", CancellationToken.None);
        var run2 = await Parser().ParseAsync(Message(), [withHighIdFirst, lowId], "es", CancellationToken.None);

        run1.MatchedRule!.Id.Should().Be(first);
        run2.MatchedRule!.Id.Should().Be(first, "the input order must not change the winner");
    }

    private static EmailImportRule RuleWithId(Guid id, string currency)
    {
        // Id has an `internal set` and the test project is an InternalsVisibleTo of the module,
        // so this is a plain assignment — no reflection.
        var rule = Rule(currency, 1);
        rule.Id = id;
        return rule;
    }

    /// <summary>The auto-commit threshold, read from its single source of truth.</summary>
    private static decimal AutoCommitThreshold =>
        EmailConnection.DefaultAutoCommitMinConfidence;
}