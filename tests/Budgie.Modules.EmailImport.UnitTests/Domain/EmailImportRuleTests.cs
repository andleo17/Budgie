using System.Text.Json;
using Budgie.Modules.EmailImport.Domain.Rules;
using Budgie.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace Budgie.Modules.EmailImport.UnitTests.Domain;

/// <summary>
/// <see cref="EmailImportRule"/> validates EVERY invariant at write time, including regex
/// compilation. That strictness is the point: an invalid rule persisted to the database
/// would fail silently at sync time, when the user is not looking, instead of at save time,
/// when they are.
/// </summary>
public class EmailImportRuleTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();

    private const string ValidPatterns = """{"amount":"Importe:\\s*(?<amount>[\\d.,]+)"}""";

    private static EmailImportRule Create(
        string? name = "Banco Ejemplo",
        string adapterKey = EmailImportRule.GenericRegexKey,
        int priority = 0,
        bool isEnabled = true,
        Guid? accountId = null,
        TransactionType type = TransactionType.Expense,
        string currency = "EUR",
        decimal? exchangeRate = null,
        string? fromAddress = null,
        string? subjectContains = null,
        string? bodyRegex = null,
        string? fieldPatternsJson = ValidPatterns,
        bool autoCommit = false) =>
        EmailImportRule.Create(
            UserId, name!, adapterKey, priority, isEnabled,
            accountId ?? AccountId, null, type, currency, exchangeRate,
            fromAddress, subjectContains, bodyRegex, fieldPatternsJson, autoCommit);

    // ── The happy path ─────────────────────────────────────────────────────────────────
    [Fact]
    public void Create_ValidRule_PopulatesEveryField()
    {
        var categoryId = Guid.NewGuid();

        var rule = EmailImportRule.Create(
            UserId, "Banco Ejemplo", EmailImportRule.GenericRegexKey, 5, true,
            AccountId, categoryId, TransactionType.Income, "USD", 1.25m,
            "@banco.es", "pago", "Importe", ValidPatterns, autoCommit: true);

        rule.Id.Should().NotBeEmpty();
        rule.UserId.Should().Be(UserId);
        rule.Name.Should().Be("Banco Ejemplo");
        rule.AdapterKey.Should().Be(EmailImportRule.GenericRegexKey);
        rule.Priority.Should().Be(5);
        rule.IsEnabled.Should().BeTrue();
        rule.AccountId.Should().Be(AccountId);
        rule.CategoryId.Should().Be(categoryId);
        rule.TransactionType.Should().Be(TransactionType.Income);
        rule.Currency.Should().Be("USD");
        rule.ExchangeRate.Should().Be(1.25m);
        rule.FromAddress.Should().Be("@banco.es");
        rule.SubjectContains.Should().Be("pago");
        rule.BodyRegex.Should().Be("Importe");
        rule.AutoCommit.Should().BeTrue();
    }

    [Fact]
    public void Create_NullExchangeRate_NormalizesToOne()
    {
        // Every rule reaches CreateTransactionCommand with a rate; null must never
        // propagate into the money arithmetic.
        Create(exchangeRate: null).ExchangeRate.Should().Be(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_NonPositiveExchangeRate_NormalizesToOne(decimal rate)
    {
        Create(exchangeRate: rate).ExchangeRate.Should().Be(1);
    }

    [Fact]
    public void Create_TrimsTextAndBlankOptionalsToNull()
    {
        var rule = Create(
            name: "  Banco Ejemplo  ", fromAddress: "   ",
            subjectContains: "", bodyRegex: "   ");

        rule.Name.Should().Be("Banco Ejemplo");
        rule.FromAddress.Should().BeNull();
        rule.SubjectContains.Should().BeNull();
        rule.BodyRegex.Should().BeNull();
    }

    // ── TransactionType: only Income and Expense ───────────────────────────────────────
    [Theory]
    [InlineData(TransactionType.Transfer)]
    [InlineData(TransactionType.Loan)]
    [InlineData(TransactionType.Repayment)]
    public void Create_UnsupportedTransactionType_IsRejected(TransactionType type)
    {
        // An email can never supply the second account a Transfer needs, nor the person a
        // Loan or Repayment needs.
        var act = () => Create(type: type);

        act.Should().Throw<ArgumentException>()
            .Which.Message.Should().Contain("Only Income and Expense are allowed");
    }

    // ── generic.regex requires field patterns ──────────────────────────────────────────
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_GenericRegexWithoutFieldPatterns_IsRejected(string? patterns)
    {
        var act = () => Create(fieldPatternsJson: patterns);

        act.Should().Throw<ArgumentException>()
            .Which.Message.Should().Contain("requires a non-empty FieldPatterns JSON");
    }

    [Fact]
    public void Create_AnotherAdapterWithoutFieldPatterns_IsAllowed()
    {
        // The requirement is tied to generic.regex, not to every adapter.
        var act = () => Create(adapterKey: "otro.adapter", fieldPatternsJson: null);
        act.Should().NotThrow();
    }

    // ── Regex compilation ──────────────────────────────────────────────────────────────
    [Fact]
    public void Create_InvalidRegexInFieldPatterns_IsRejected()
    {
        var act = () => Create(fieldPatternsJson: """{"amount":"[unclosed"}""");

        act.Should().Throw<ArgumentException>()
            .Which.Message.Should().Contain("FieldPatterns.amount");
    }

    [Fact]
    public void Create_InvalidBodyRegex_IsRejected()
    {
        var act = () => Create(bodyRegex: "(unclosed");

        act.Should().Throw<ArgumentException>()
            .Which.Message.Should().Contain("BodyRegex");
    }

    [Fact]
    public void Create_MalformedFieldPatternsJson_IsRejected()
    {
        var act = () => Create(fieldPatternsJson: "{ not json");

        act.Should().Throw<ArgumentException>()
            .Which.Message.Should().Contain("not valid JSON");
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("\"texto\"")]
    [InlineData("42")]
    public void Create_FieldPatternsJsonThatIsNotAnObject_IsRejected(string json)
    {
        var act = () => Create(fieldPatternsJson: json);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_NonStringFieldPatternValue_IsIgnoredRatherThanRejected()
    {
        // A stray null/number is skipped instead of crashing validation; the amount pattern
        // is what matters and it is still validated.
        var act = () => Create(fieldPatternsJson: """{"amount":"[0-9]+","count":3}""");

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("amount", "[0-9]+")]
    [InlineData("date", @"\d{2}/\d{2}/\d{4}")]
    [InlineData("merchant", ".+")]
    [InlineData("direction", "abono")]
    [InlineData("symbol", "€")]
    public void Create_EveryDocumentedPatternKey_IsAccepted(string key, string pattern)
    {
        var json = JsonSerializer.Serialize(new Dictionary<string, string> { [key] = pattern });

        var act = () => Create(fieldPatternsJson: json);

        act.Should().NotThrow();
    }

    // ── Field bounds and required values ───────────────────────────────────────────────
    [Fact]
    public void Create_EmptyName_IsRejected()
    {
        var act = () => Create(name: "   ");
        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("name");
    }

    [Fact]
    public void Create_NameOverOneHundredCharacters_IsRejected()
    {
        var act = () => Create(name: new string('a', 101));
        act.Should().Throw<ArgumentException>().Which.Message.Should().Contain("100");
    }

    [Fact]
    public void Create_EmptyAdapterKey_IsRejected()
    {
        var act = () => Create(adapterKey: "  ");
        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("adapterKey");
    }

    [Fact]
    public void Create_AdapterKeyOverFiftyCharacters_IsRejected()
    {
        var act = () => Create(adapterKey: new string('a', 51));
        act.Should().Throw<ArgumentException>().Which.Message.Should().Contain("50");
    }

    [Fact]
    public void Create_NegativePriority_IsRejected()
    {
        var act = () => Create(priority: -1);
        act.Should().Throw<ArgumentException>().Which.Message.Should().Contain("negative");
    }

    [Fact]
    public void Create_ZeroPriority_IsAccepted()
    {
        var act = () => Create(priority: 0);
        act.Should().NotThrow();
    }

    [Fact]
    public void Create_EmptyAccountId_IsRejected()
    {
        var act = () => Create(accountId: Guid.Empty);
        act.Should().Throw<ArgumentException>().Which.Message.Should().Contain("AccountId");
    }

    [Theory]
    [InlineData("EURO")]
    [InlineData("E")]
    [InlineData("1234")]
    [InlineData("")]
    public void Create_InvalidCurrency_IsRejected(string currency)
    {
        var act = () => Create(currency: currency);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_OverlongFromAddress_IsRejected()
    {
        var act = () => Create(fromAddress: "@" + new string('a', 256));
        act.Should().Throw<ArgumentException>().Which.Message.Should().Contain("255");
    }

    [Fact]
    public void Create_OverlongSubjectContains_IsRejected()
    {
        var act = () => Create(subjectContains: new string('a', 201));
        act.Should().Throw<ArgumentException>().Which.Message.Should().Contain("200");
    }

    [Fact]
    public void Create_OverlongBodyRegex_IsRejectedBeforeCompilation()
    {
        var act = () => Create(bodyRegex: new string('a', 501));
        act.Should().Throw<ArgumentException>().Which.Message.Should().Contain("500");
    }

    // ── Filters ────────────────────────────────────────────────────────────────────────
    [Fact]
    public void MatchesFilters_NoFiltersAtAll_MatchesEverything()
    {
        var rule = Create();

        rule.MatchesFilters("cualquiera@otro.es", "otro asunto", "cualquier cuerpo").Should().BeTrue();
    }

    [Theory]
    [InlineData("notificaciones@banco.es", true)]
    [InlineData("NOTIFICACIONES@BANCO.ES", true)]     // case-insensitive
    [InlineData("\"Banco Ejemplo\" <notificaciones@banco.es>", true)]
    [InlineData("notificaciones@otro.es", false)]
    [InlineData("otra@banco.es", false)]
    public void MatchesFilters_AddressFilter_MatchesTheBareAddress(string sender, bool expected)
    {
        Create(fromAddress: "notificaciones@banco.es").MatchesFilters(sender, null, "").Should().Be(expected);
    }

    [Theory]
    [InlineData("alerts@alerts.banco.es", "@banco.es", true)]     // domain suffix
    [InlineData("alerts@sub.banco.es", "banco.es", true)]        // sender domain is below the filter
    [InlineData("alerts@banco.es", "sub.banco.es", true)]        // the filter names a subdomain
    [InlineData("alerts@x.banco.es", "@.banco.es", true)]        // explicit dot suffix
    [InlineData("alerts@banco.es", "@.banco.es", false)]         // the apex is not a ".banco.es"
    [InlineData("alerts@banco.com", "@banco.es", false)]         // different TLD
    [InlineData("alerts@notbanco.es", "@banco.es", false)]       // not a domain boundary
    [InlineData("n@sub.dominio.es", "@dominio.es", true)]        // any subdomain of the filter
    public void MatchesFilters_DomainFilter_MatchesOnTheDomainPart(
        string sender, string filter, bool expected)
    {
        Create(fromAddress: filter).MatchesFilters(sender, null, "").Should().Be(expected);
    }

    [Fact]
    public void MatchesFilters_AddressFilterWithAnEmptySender_DoesNotMatch()
    {
        Create(fromAddress: "@banco.es").MatchesFilters("   ", null, "").Should().BeFalse();
    }

    [Theory]
    [InlineData("Su pago de 45,20", true)]
    [InlineData("SU PAGO", true)]        // case-insensitive
    [InlineData("Reembolso", false)]
    [InlineData(null, false)]            // a filter set means a missing subject can never match
    public void MatchesFilters_SubjectContains_FiltersOnSubstring(string? subject, bool expected)
    {
        Create(subjectContains: "pago").MatchesFilters("a@b.c", subject, "").Should().Be(expected);
    }

    [Theory]
    [InlineData("Importe: 45,20", true)]
    [InlineData("importe: 45,20", true)]   // case-insensitive
    [InlineData("Nada que ver", false)]
    public void MatchesFilters_BodyRegex_RunsAgainstThePlainText(string body, bool expected)
    {
        Create(bodyRegex: "Importe").MatchesFilters("a@b.c", null, body).Should().Be(expected);
    }

    [Fact]
    public void MatchesFilters_AllThreeFiltersPresent_RequiresAllOfThemToMatch()
    {
        var rule = Create(fromAddress: "@banco.es", subjectContains: "pago", bodyRegex: "Importe");

        rule.MatchesFilters("n@banco.es", "su pago", "Importe: 1").Should().BeTrue();
        // The BodyRegex runs with IgnoreCase, so a lowercase hit counts as a match too —
        // the AND is about the three filters, not about case sensitivity.
        rule.MatchesFilters("n@banco.es", "su pago", "sin importe").Should().BeTrue();
        rule.MatchesFilters("n@banco.es", "su pago", "otra cosa").Should().BeFalse();
        rule.MatchesFilters("n@otro.es", "su pago", "Importe: 1").Should().BeFalse();
        rule.MatchesFilters("n@banco.es", "otro", "Importe: 1").Should().BeFalse();
    }

    [Fact]
    public void MatchesFilters_BodyRegexWithNullBody_DoesNotMatch()
    {
        Create(bodyRegex: "Importe").MatchesFilters("a@b.c", null, null!).Should().BeFalse();
    }

    // ── Update: same validation, and ownership never changes ───────────────────────────
    [Fact]
    public void Update_ValidChange_ReplacesEveryMutableField()
    {
        var rule = Create();
        var originalId = rule.Id;

        rule.Update(
            "Otro banco", EmailImportRule.GenericRegexKey, 9, false,
            AccountId, null, TransactionType.Income, "USD", 2m,
            "@otro.es", "nuevo", "Otro", ValidPatterns, autoCommit: true);

        rule.Id.Should().Be(originalId);       // identity is stable
        rule.UserId.Should().Be(UserId);       // ownership NEVER changes
        rule.Name.Should().Be("Otro banco");
        rule.Priority.Should().Be(9);
        rule.IsEnabled.Should().BeFalse();
        rule.TransactionType.Should().Be(TransactionType.Income);
        rule.Currency.Should().Be("USD");
        rule.ExchangeRate.Should().Be(2m);
        rule.AutoCommit.Should().BeTrue();
    }

    [Fact]
    public void Update_InvalidChange_ThrowsAndLeavesTheRuleUntouched()
    {
        var rule = Create();

        var act = () => rule.Update(
            "Otro", EmailImportRule.GenericRegexKey, 0, true, AccountId, null,
            TransactionType.Transfer, "EUR", null, null, null, null, ValidPatterns, false);

        act.Should().Throw<ArgumentException>();
        rule.Name.Should().Be("Banco Ejemplo");          // nothing was applied
        rule.TransactionType.Should().Be(TransactionType.Expense);
    }

    [Fact]
    public void Update_NullExchangeRate_NormalizesToOne()
    {
        var rule = Create(exchangeRate: 3m);

        rule.Update("n", EmailImportRule.GenericRegexKey, 0, true, AccountId, null,
            TransactionType.Expense, "EUR", null, null, null, null, ValidPatterns, false);

        rule.ExchangeRate.Should().Be(1);
    }

    [Fact]
    public void Update_BodyRegexChange_InvalidatesTheCompiledRegex()
    {
        var rule = Create(bodyRegex: "Importe");
        rule.MatchesFilters("a@b.c", null, "Importe: 1").Should().BeTrue();

        rule.Update("n", EmailImportRule.GenericRegexKey, 0, true, AccountId, null,
            TransactionType.Expense, "EUR", null, null, null, "Reembolso", ValidPatterns, false);

        // The cached regex must NOT survive the change.
        rule.MatchesFilters("a@b.c", null, "Importe: 1").Should().BeFalse();
        rule.MatchesFilters("a@b.c", null, "Reembolso: 1").Should().BeTrue();
    }

    [Fact]
    public void GenericRegexKey_IsGenericDotRegex()
    {
        EmailImportRule.GenericRegexKey.Should().Be("generic.regex");
    }
}
