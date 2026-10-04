using System.Text.Json;
using Budgie.Modules.EmailImport.Application.Parsing;
using Budgie.Modules.EmailImport.Application.Parsing.Adapters;
using Budgie.Modules.EmailImport.Domain.Connections;
using Budgie.Modules.EmailImport.Domain.Parsing;
using Budgie.Modules.EmailImport.Domain.Rules;
using Budgie.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace Budgie.Modules.EmailImport.UnitTests.Parsing;

/// <summary>
/// Slice 3 (§2.3, option (c)) at the level where <see cref="ParseOutcome.RuleRejected"/> lives:
/// the adapter's verdict. The parser consumes that flag and always returns a terminal outcome
/// (see <see cref="EmailParserCurrencyRejectionTests"/>), so these are the tests that pin "a
/// contradicting currency can never be scored into an auto-commit".
/// </summary>
public class GenericRegexAdapterCurrencyRejectionTests
{
    private static readonly Guid AccountId = Guid.NewGuid();

    private static EmailParseContext Context(string plainText, string currency, string amountPattern)
    {
        var patterns = new Dictionary<string, string?> { ["amount"] = amountPattern };
        var rule = EmailImportRule.Create(
            Guid.NewGuid(), "regla", EmailImportRule.GenericRegexKey, 1, true,
            AccountId, null, TransactionType.Expense, currency, null,
            null, null, null, JsonSerializer.Serialize(patterns), autoCommit: true);

        return new EmailParseContext(
            EmailMessage.Create("m1", "t1", "banco@ejemplo.es", "aviso", "raw",
                new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero)),
            plainText,
            rule,
            "es");
    }

    [Theory]
    [InlineData("Importe: 45,20 $", "USD")]   // the original defect: dollars against a EUR rule
    [InlineData("Importe: 45,20 £", "GBP")]
    [InlineData("Importe: 45,20 GBP", "GBP")] // the textual form, not just the glyph
    public void Parse_SymbolMapsToAKnownCodeOtherThanTheRules_IsRejected(
        string text, string symbolIso)
    {
        var outcome = new GenericRegexAdapter().Parse(
            Context(text, "EUR", @"Importe:\s*(?<amount>[\d.,]+)\s*(?<symbol>[^\s]+)"));

        outcome.RuleRejected.Should().BeTrue($"{symbolIso} contradicts a EUR rule: a fact, not a score");
        outcome.Matched.Should().BeFalse();
        outcome.Value.Should().BeNull("a rejected message must not carry a transaction anyone could stage");
        outcome.SkipReason.Should().Be(GenericRegexAdapter.CurrencyContradictionReason);
    }

    [Fact]
    public void Parse_SymbolIsUnknown_StaysAReviewablePenaltyInsteadOfARejection()
    {
        // "CHF" is not in the symbol table. Rejecting here would stop importing for a user with a
        // CHF account over a gap in our own table: we do not know the currency is wrong, we only
        // know we cannot identify it.
        var outcome = new GenericRegexAdapter().Parse(
            Context("Importe: 45,20 CHF", "EUR", @"Importe:\s*(?<amount>[\d.,]+)\s*(?<symbol>[^\s]+)"));

        outcome.RuleRejected.Should().BeFalse("an unmappable symbol is our ignorance, not a contradiction");
        outcome.Matched.Should().BeTrue();
        outcome.Value!.FieldConfidence[ConfidenceScorer.CurrencyField].Should().Be(0m);
        outcome.Value.Confidence.Should().BeLessThan(
            EmailConnection.DefaultAutoCommitMinConfidence,
            "an unidentified currency symbol must keep the message below the auto-commit threshold");
    }

    [Theory]
    [InlineData("Importe: 45,20 €")]      // the glyph
    [InlineData("Importe: 45,20 EUR")]    // the textual form
    public void Parse_SymbolAgreesWithTheRule_IsAcceptedWithFullCurrencyConfidence(string text)
    {
        var outcome = new GenericRegexAdapter().Parse(
            Context(text, "EUR", @"Importe:\s*(?<amount>[\d.,]+)\s*(?<symbol>[^\s]+)"));

        outcome.RuleRejected.Should().BeFalse();
        outcome.Matched.Should().BeTrue();
        outcome.Value!.FieldConfidence[ConfidenceScorer.CurrencyField].Should().Be(1m);
    }

    [Fact]
    public void Parse_RuleDeclaresDollarsAndTheSymbolIsDollars_IsNotRejected()
    {
        // The mirror image of the defect: a rejection rule that fired on an agreeing symbol would
        // break every legitimate USD rule.
        var outcome = new GenericRegexAdapter().Parse(
            Context("Importe: 45,20 $", "USD", @"Importe:\s*(?<amount>[\d.,]+)\s*(?<symbol>[^\s]+)"));

        outcome.RuleRejected.Should().BeFalse();
        outcome.Matched.Should().BeTrue();
        outcome.Value!.FieldConfidence[ConfidenceScorer.CurrencyField].Should().Be(1m);
    }

    [Fact]
    public void RuleCannotOwn_CarriesTheReasonAndSetsTheFlag()
    {
        var outcome = ParseOutcome.RuleCannotOwn("currency-symbol-contradiction");

        outcome.RuleRejected.Should().BeTrue();
        outcome.Matched.Should().BeFalse();
        outcome.Value.Should().BeNull();
        outcome.SkipReason.Should().Be("currency-symbol-contradiction");
    }

    [Fact]
    public void RuleCannotOwn_WithoutReason_Throws()
    {
        var act = () => ParseOutcome.RuleCannotOwn("  ");

        act.Should().Throw<ArgumentException>(
            "a rejection with no reason is indistinguishable from a bug in the adapter");
    }
}