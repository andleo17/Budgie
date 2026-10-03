using System.Text.Json;
using Budgie.Modules.EmailImport.Application.Abstractions;
using Budgie.Modules.EmailImport.Application.Parsing;
using Budgie.Modules.EmailImport.Application.Parsing.Adapters;
using Budgie.Modules.EmailImport.Domain.Parsing;
using Budgie.Modules.EmailImport.Domain.Rules;
using Budgie.Shared.Enums;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.EmailImport.UnitTests.Parsing;

/// <summary>
/// <see cref="EmailParser"/> owns rule SELECTION: the first ENABLED rule, in ascending
/// priority, whose three filters match wins — and the plain text is extracted exactly once
/// per message, no matter how many rules are evaluated.
/// </summary>
public class EmailParserTests
{
    private static readonly Guid AccountId = Guid.NewGuid();

    private readonly IMessageTextExtractor _extractor = Substitute.For<IMessageTextExtractor>();
    private readonly BankAdapterRegistry _registry;

    public EmailParserTests() => _registry = new BankAdapterRegistry([new GenericRegexAdapter()]);

    private EmailParser Parser() => new(_extractor, _registry);

    private static EmailImportRule Rule(
        string name,
        int priority,
        bool isEnabled = true,
        string? fromAddress = null,
        string? subjectContains = null,
        string? bodyRegex = null,
        string amountPattern = @"Importe:\s*(?<amount>[\d.,]+)") =>
        EmailImportRule.Create(
            Guid.NewGuid(), name, EmailImportRule.GenericRegexKey, priority, isEnabled,
            AccountId, null, TransactionType.Expense, "EUR", null,
            fromAddress, subjectContains, bodyRegex,
            FieldPatterns(amountPattern),
            autoCommit: false);

    /// <summary>Serialises the field patterns so backslashes never need manual JSON escaping.</summary>
    private static string FieldPatterns(
        string? amount = null, string? date = null, string? merchant = null)
    {
        var map = new Dictionary<string, string?>();
        if (amount is not null) map["amount"] = amount;
        if (date is not null) map["date"] = date;
        if (merchant is not null) map["merchant"] = merchant;
        return JsonSerializer.Serialize(map);
    }

    private static EmailMessage Message(
        string from = "notificaciones@banco.es",
        string subject = "Su pago",
        string body = "raw body") =>
        EmailMessage.Create("m1", "t1", from, subject, body,
            new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero));

    private void ExtractorReturns(string plainText) => _extractor
        .ExtractPlainText(Arg.Any<EmailMessage>()).Returns(plainText);

    // ── Rule selection ─────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ParseAsync_SingleMatchingRule_DelegatesToItsAdapter()
    {
        ExtractorReturns("Importe: 45,20 €");

        var outcome = await Parser().ParseAsync(
            Message(), [Rule("única", 0)], "es", CancellationToken.None);

        outcome.Matched.Should().BeTrue();
        outcome.Value!.Amount.Should().Be(45.20m);
    }

    [Fact]
    public async Task ParseAsync_LowestPriorityWins_AmongSeveralMatchingRules()
    {
        ExtractorReturns("Importe: 45,20 €");
        var first = Rule("prioridad-1", 1);
        var second = Rule("prioridad-9", 9);

        var outcome = await Parser().ParseAsync(
            Message(), [second, first], "es", CancellationToken.None);

        outcome.MatchedRule.Should().BeSameAs(first);
    }

    [Fact]
    public async Task ParseAsync_DisabledRuleIsIgnored_EvenWhenItHasTheLowestPriority()
    {
        ExtractorReturns("Importe: 45,20 €");
        var disabled = Rule("deshabilitada", 0, isEnabled: false);
        var enabled = Rule("habilitada", 5);

        var outcome = await Parser().ParseAsync(
            Message(), [disabled, enabled], "es", CancellationToken.None);

        outcome.MatchedRule.Should().BeSameAs(enabled);
    }

    [Fact]
    public async Task ParseAsync_NoRuleMatches_ReturnsNoMatchWithTheDocumentedReason()
    {
        ExtractorReturns("Importe: 45,20 €");

        var outcome = await Parser().ParseAsync(
            Message(), [Rule("otra", 0, subjectContains: "Reembolso")], "es", CancellationToken.None);

        outcome.Matched.Should().BeFalse();
        outcome.Value.Should().BeNull();
        outcome.MatchedRule.Should().BeNull();
        outcome.SkipReason.Should().Be("no-rule-matched");
    }

    [Fact]
    public async Task ParseAsync_NoRulesAtAll_ReturnsNoMatch()
    {
        ExtractorReturns("Importe: 45,20 €");

        var outcome = await Parser().ParseAsync(
            Message(), [], "es", CancellationToken.None);

        outcome.Matched.Should().BeFalse();
        outcome.SkipReason.Should().Be("no-rule-matched");
    }

    [Fact]
    public async Task ParseAsync_NonMatchingEarlierRule_DoesNotBlockTheNextOne()
    {
        // Selection continues past a rule whose filters fail — it does not stop there.
        ExtractorReturns("Importe: 45,20 €\nSaldo: 100,00 €");
        var nonMatching = Rule("por-sender", 0, fromAddress: "@otrobanco.es");
        var matching = Rule("correcta", 1);

        var outcome = await Parser().ParseAsync(
            Message(), [nonMatching, matching], "es", CancellationToken.None);

        outcome.MatchedRule.Should().BeSameAs(matching);
    }

    // ── Filters (evaluated by the domain, not reimplemented here) ──────────────────────
    [Fact]
    public async Task ParseAsync_FromAddressFilter_ReceivesTheSenderAsSupplied()
    {
        ExtractorReturns("Importe: 45,20 €");
        var rule = Rule("filtro", 0, fromAddress: "notificaciones@banco.es");

        (await Parser().ParseAsync(Message(), [rule], "es", CancellationToken.None))
            .Matched.Should().BeTrue();
    }

    [Fact]
    public async Task ParseAsync_SubjectFilter_RejectsADifferentSubject()
    {
        ExtractorReturns("Importe: 45,20 €");
        var rule = Rule("filtro", 0, subjectContains: "Reembolso");

        (await Parser().ParseAsync(
                Message(subject: "Su pago"), [rule], "es", CancellationToken.None))
            .Matched.Should().BeFalse();
    }

    [Fact]
    public async Task ParseAsync_BodyRegexFilter_RunsAgainstTheExtractedPlainText()
    {
        ExtractorReturns("Importe: 45,20 €");
        var matching = Rule("con-regex", 0, bodyRegex: "Importe");
        var failing = Rule("sin-regex", 1, bodyRegex: "Reembolso");

        (await Parser().ParseAsync(Message(), [failing, matching], "es", CancellationToken.None))
            .MatchedRule.Should().BeSameAs(matching);
    }

    [Fact]
    public async Task ParseAsync_AllThreeFiltersMustMatch()
    {
        // AND semantics: FromAddress matches, SubjectContains matches, BodyRegex does not.
        ExtractorReturns("Importe: 45,20 €");
        var rule = Rule("and", 0,
            fromAddress: "@banco.es", subjectContains: "pago", bodyRegex: "Reembolso");

        (await Parser().ParseAsync(Message(), [rule], "es", CancellationToken.None))
            .Matched.Should().BeFalse();
    }

    // ── The extraction happens exactly once ────────────────────────────────────────────
    [Fact]
    public async Task ParseAsync_ExtractsPlainTextExactlyOncePerMessage()
    {
        // MIME decoding and HTML conversion dominate the parse cost; extracting per rule is
        // the difference that matters once a batch carries tens of messages.
        ExtractorReturns("Importe: 45,20 €");
        var rules = new[]
        {
            Rule("a", 0, fromAddress: "@otro.es"),
            Rule("b", 1, fromAddress: "@otro.es"),
            Rule("c", 2)
        };

        await Parser().ParseAsync(Message(), rules, "es", CancellationToken.None);

        _extractor.Received(1).ExtractPlainText(Arg.Any<EmailMessage>());
    }

    [Fact]
    public async Task ParseAsync_AdaptsTheExtractedTextNotTheRawBody()
    {
        // The regexes run against what the extractor returned, never against message.Body:
        // for a raw MIME message the raw body is headers and base64.
        ExtractorReturns("Importe: 45,20 €");
        var rule = Rule("importe", 0);

        var outcome = await Parser().ParseAsync(
            Message(body: "Content-Transfer-Encoding: base64\r\n\r\naW1wb3J0ZQ=="),
            [rule], "es", CancellationToken.None);

        outcome.Matched.Should().BeTrue();
        outcome.Value!.Amount.Should().Be(45.20m);
    }

    // ── The language code is passed through ────────────────────────────────────────────
    [Theory]
    [InlineData("es", 2026, 2, 1)]
    [InlineData("en", 2026, 1, 2)]
    public async Task ParseAsync_PassesTheUserLanguageToTheAdapter(
        string language, int year, int month, int day)
    {
        ExtractorReturns("Date: 01/02/2026\nImporte: 45,20");
        var rule = EmailImportRule.Create(
            Guid.NewGuid(), "fechas", EmailImportRule.GenericRegexKey, 0, true,
            AccountId, null, TransactionType.Expense, "EUR", null,
            fromAddress: null, subjectContains: null, bodyRegex: null,
            fieldPatternsJson: FieldPatterns(
                amount: @"[0-9]+,[0-9]{2}", date: @"Date:\s*(?<date>[0-9/]+)"),
            autoCommit: false);

        var outcome = await Parser().ParseAsync(
            Message(), [rule], language, CancellationToken.None);

        outcome.Matched.Should().BeTrue();
        outcome.Value!.Date.Should().Be(new DateTime(year, month, day));
    }

    // ── Guards ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ParseAsync_NullMessage_Throws()
    {
        var act = () => Parser().ParseAsync(null!, [], "es", CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ParseAsync_NullRules_Throws()
    {
        var act = () => Parser().ParseAsync(Message(), null!, "es", CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ParseAsync_AlreadyCancelledToken_ThrowsBeforeDoingAnything()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => Parser().ParseAsync(Message(), [], "es", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _extractor.DidNotReceive().ExtractPlainText(Arg.Any<EmailMessage>());
    }
}
