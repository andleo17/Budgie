using Budgie.Modules.EmailImport.Application.Abstractions;
using Budgie.Modules.EmailImport.Application.Parsing;
using Budgie.Modules.EmailImport.Application.Parsing.Adapters;
using Budgie.Modules.EmailImport.Domain.Parsing;
using Budgie.Modules.EmailImport.Domain.Rules;
using Budgie.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace Budgie.Modules.EmailImport.UnitTests.Parsing;

/// <summary>
/// The regex cache used to be keyed on <c>RuleId</c> alone, so editing a rule kept serving
/// the previously compiled patterns: a user who fixed a broken regex kept getting the old
/// ones and the sync silently mis-parsed until the process restarted. The existing
/// "same rule compiles once" test did not catch it because it never changed the patterns.
/// </summary>
public class GenericRegexAdapterCacheInvalidationTests
{
    private const string BasePatterns =
        """{"amount":"Importe:\\s*([\\d.,]+)","merchant":"Comercio:\\s*(.+)"}""";

    private static EmailImportRule Rule(string patternsJson) =>
        EmailImportRule.Create(
            userId: Guid.NewGuid(),
            name: "Regla",
            adapterKey: EmailImportRule.GenericRegexKey,
            priority: 100,
            isEnabled: true,
            accountId: Guid.NewGuid(),
            categoryId: null,
            transactionType: TransactionType.Expense,
            currency: "EUR",
            exchangeRate: null,
            fromAddress: null,
            subjectContains: null,
            bodyRegex: null,
            fieldPatternsJson: patternsJson,
            autoCommit: true);

    private static EmailMessage Message() => new(
        MessageId: "m1",
        ThreadId: null,
        FromAddress: "banco@example.com",
        Subject: "Aviso",
        Body: "Importe: 45,20 EUR\nComercio: ACME",
        ReceivedAt: DateTimeOffset.UtcNow);

    private static EmailParseContext Context(EmailImportRule rule, string languageCode = "es") =>
        new(Message(), "Importe: 45,20 EUR\nComercio: ACME", rule, languageCode);

    [Fact]
    public void EditingThePatternsOfARule_RecompilesInsteadOfServingStalePatterns()
    {
        var adapter = new GenericRegexAdapter();
        var rule = Rule(BasePatterns);

        adapter.Parse(Context(rule)).Should().Match<ParseOutcome>(o => o.Matched && o.Value!.Amount == 45.20m);
        var compilationsAfterFirst = adapter.CompilationCount;
        compilationsAfterFirst.Should().Be(1);

        // Same rule id (Update persists the same id), different patterns: the user fixed a regex.
        var otherPatterns =
            """{"amount":"Total:\\s*([\\d.,]+)","merchant":"Comercio:\\s*(.+)"}""";
        rule.Update(
            adapterKey: EmailImportRule.GenericRegexKey,
            name: "Regla",
            priority: 100,
            isEnabled: true,
            accountId: rule.AccountId,
            categoryId: null,
            transactionType: TransactionType.Expense,
            currency: "EUR",
            exchangeRate: null,
            fromAddress: null,
            subjectContains: null,
            bodyRegex: null,
            fieldPatternsJson: otherPatterns,
            autoCommit: true);

        // The new pattern must NOT match the same body: if the cache served the stale
        // CompiledRuleSet, this would still extract 45.20 from "Importe:".
        var outcome = adapter.Parse(Context(rule));

        outcome.Matched.Should().BeFalse();
        outcome.SkipReason.Should().Be("amount-not-found");
        adapter.CompilationCount.Should().Be(2);
    }

    [Fact]
    public void UnchangedPatterns_StillHitTheCache()
    {
        var adapter = new GenericRegexAdapter();
        var rule = Rule(BasePatterns);

        adapter.Parse(Context(rule));
        adapter.Parse(Context(rule));

        adapter.CompilationCount.Should().Be(1);
    }

    [Fact]
    public void TwoRulesWithIdenticalPatterns_DoNotShareAnEntry()
    {
        var adapter = new GenericRegexAdapter();

        adapter.Parse(Context(Rule(BasePatterns)));
        adapter.Parse(Context(Rule(BasePatterns)));

        // Same patterns but different rule ids: distinct keys, so both compile.
        adapter.CompilationCount.Should().Be(2);
    }
}
