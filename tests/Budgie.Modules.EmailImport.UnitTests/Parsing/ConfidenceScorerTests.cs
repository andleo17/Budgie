using Budgie.Modules.EmailImport.Application.Parsing;
using FluentAssertions;
using Xunit;

namespace Budgie.Modules.EmailImport.UnitTests.Parsing;

/// <summary>
/// <see cref="ConfidenceScorer"/> is the weighted sum over <see cref="ConfidenceWeights"/>,
/// plus <c>Explain()</c> — the per-field breakdown the review UI shows. A bare number cannot
/// tell a user <i>why</i> an email went to review, so both are asserted.
/// </summary>
/// <remarks>
/// <b>Two numbers in the design spec §4.5 examples do not match the code</b>, and the code is
/// what ships. Both differences are pinned in the tests marked "spec vs code" below:
/// <list type="bullet">
/// <item>
/// The spec writes <c>0.35 + 0.20 + 0.18 + 0.15 + 0.10 = 0.98</c> for a perfect parse. But
/// the merchant weight is <c>0.20</c>, not <c>0.18</c>, so a perfect parse scores exactly
/// <c>1.00</c>, and the merchant can only ever be <c>1.0 / 0.3 / 0.0</c> — <c>0.9</c> does
/// not exist in <see cref="GenericRegexAdapter"/>, so no input can produce <c>0.98</c>.
/// </item>
/// <item>
/// The spec writes "<c>amount = 0.3</c> → total ≈ <c>0.72</c>". With the real weights and all
/// other fields at their maximum the total is <c>0.755</c>, which rounds to <c>0.76</c>.
/// <c>0.72</c> is not reachable.
/// </item>
/// </list>
/// Both are pinned as documentation of the real arithmetic so nobody "fixes" the scorer to
/// match a stale example.
/// </remarks>
public class ConfidenceScorerTests
{
    private static Dictionary<string, decimal> Scores(
        decimal amount = 1m, decimal direction = 1m, decimal merchant = 1m,
        decimal date = 1m, decimal currency = 1m) => new()
    {
        [ConfidenceScorer.AmountField] = amount,
        [ConfidenceScorer.DirectionField] = direction,
        [ConfidenceScorer.MerchantField] = merchant,
        [ConfidenceScorer.DateField] = date,
        [ConfidenceScorer.CurrencyField] = currency
    };

    // ── The weights themselves ─────────────────────────────────────────────────────────
    [Fact]
    public void Weights_SumToExactlyOne()
    {
        (ConfidenceWeights.Amount + ConfidenceWeights.Direction + ConfidenceWeights.Merchant
         + ConfidenceWeights.Date + ConfidenceWeights.Currency).Should().Be(1.0m);
    }

    [Fact]
    public void Weights_MatchTheDesignSpecTable()
    {
        ConfidenceWeights.Amount.Should().Be(0.35m);
        ConfidenceWeights.Direction.Should().Be(0.20m);
        ConfidenceWeights.Merchant.Should().Be(0.20m);
        ConfidenceWeights.Date.Should().Be(0.15m);
        ConfidenceWeights.Currency.Should().Be(0.10m);
    }

    [Fact]
    public void DefaultAutoCommitThreshold_IsEightyFiveHundredths()
    {
        ConfidenceWeights.DefaultAutoCommitThreshold.Should().Be(0.85m);
    }

    // ── The arithmetic ────────────────────────────────────────────────────────────────
    [Fact]
    public void Compute_AllFieldsPerfect_ScoresExactlyOne()
    {
        // spec vs code: the spec's "0.98" example assumed a merchant weight of 0.18. The
        // shipped weight is 0.20, so the true maximum is 1.00.
        ConfidenceScorer.Compute(Scores()).Should().Be(1.0m);
    }

    [Fact]
    public void Compute_AllFieldsZero_ScoresZero()
    {
        ConfidenceScorer.Compute(Scores(0m, 0m, 0m, 0m, 0m)).Should().Be(0m);
    }

    [Fact]
    public void Compute_SingleFieldScore_CostsExactlyItsWeight()
    {
        // "A missing field costs its weight, never is silently ignored."
        ConfidenceScorer.Compute(Scores(amount: 0m)).Should().Be(1m - ConfidenceWeights.Amount);
        ConfidenceScorer.Compute(Scores(direction: 0m)).Should().Be(1m - ConfidenceWeights.Direction);
        ConfidenceScorer.Compute(Scores(merchant: 0m)).Should().Be(1m - ConfidenceWeights.Merchant);
        ConfidenceScorer.Compute(Scores(date: 0m)).Should().Be(1m - ConfidenceWeights.Date);
        ConfidenceScorer.Compute(Scores(currency: 0m)).Should().Be(1m - ConfidenceWeights.Currency);
    }

    [Fact]
    public void Compute_AbsentFieldKey_CountsAsZero()
    {
        // An unscored field must cost its weight.
        var partial = new Dictionary<string, decimal>
        {
            [ConfidenceScorer.AmountField] = 1m
        };

        ConfidenceScorer.Compute(partial).Should().Be(ConfidenceWeights.Amount);
    }

    [Fact]
    public void Compute_ThreeAmountMatchesAndEverythingElsePerfect_ScoresSevenHundredFiftyFiveThousandths()
    {
        // spec vs code: the spec's "~0.72" is not reachable with these weights. The real value
        // is 0.35*0.3 + 0.20 + 0.20 + 0.15 + 0.10 = 0.755, comfortably below the 0.85
        // auto-commit threshold — which is the whole point of the penalty.
        var confidence = ConfidenceScorer.Compute(Scores(amount: 0.3m));

        confidence.Should().Be(0.755m);
        confidence.Should().BeLessThan(ConfidenceWeights.DefaultAutoCommitThreshold);
    }

    [Fact]
    public void Compute_TwoAmountMatchesAndEverythingElsePerfect_ScoresZeroPointEightTwoFive()
    {
        // 0.35*0.5 + 0.20 + 0.20 + 0.15 + 0.10 = 0.825. Note this is STILL below the 0.85
        // auto-commit threshold, which is exactly why a two-match amount always reaches a
        // human even when every other field is perfect.
        var confidence = ConfidenceScorer.Compute(Scores(amount: 0.5m));

        confidence.Should().Be(0.825m);
        confidence.Should().BeLessThan(ConfidenceWeights.DefaultAutoCommitThreshold);
    }

    [Fact]
    public void Compute_OnlyTheAmountFieldScored_StaysFarBelowTheThreshold()
    {
        // The threshold genuinely requires the rest of the fields: an amount alone can never
        // auto-commit, no matter how good it is.
        var amountOnly = new Dictionary<string, decimal>
        {
            [ConfidenceScorer.AmountField] = 1m
        };

        ConfidenceScorer.Compute(amountOnly).Should().BeLessThan(ConfidenceWeights.DefaultAutoCommitThreshold);
    }

    [Fact]
    public void Compute_BoilerplateMerchantAndEverythingElsePerfect_ScoresZeroPointEightSix()
    {
        // 1.00 - 0.20*0.7 = 0.86 — the narrowest auto-commit margin in the whole system: a
        // boilerplate merchant ALONE still auto-commits. Pinned because a weight tweak, or a
        // new penalty on boilerplate merchants, would flip it silently.
        var confidence = ConfidenceScorer.Compute(Scores(merchant: 0.3m));

        confidence.Should().Be(0.86m);
        confidence.Should().BeGreaterThan(ConfidenceWeights.DefaultAutoCommitThreshold);
    }

    [Fact]
    public void Compute_HalfAmountPlusBoilerplateMerchant_FallsBelowThreshold()
    {
        // 0.35*0.5 + 0.20 + 0.20*0.3 + 0.15 + 0.10 = 0.685 → review.
        ConfidenceScorer.Compute(Scores(amount: 0.5m, merchant: 0.3m)).Should().Be(0.685m);
    }

    [Fact]
    public void Compute_ScoreAboveOne_IsClampedToOne()
    {
        // A buggy adapter must never be able to inflate the confidence past the threshold.
        ConfidenceScorer.Compute(Scores(amount: 5m)).Should().Be(1.0m);
    }

    [Fact]
    public void Compute_NegativeScore_IsClampedToZero()
    {
        ConfidenceScorer.Compute(Scores(merchant: -1m)).Should().Be(
            1m - ConfidenceWeights.Merchant);
    }

    [Fact]
    public void Compute_NullDictionary_Throws()
    {
        var act = () => ConfidenceScorer.Compute(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ── Explain(): one line per field, in a stable order ───────────────────────────────
    [Fact]
    public void Explain_ReturnsOneLinePerFieldInAStableOrder()
    {
        var lines = ConfidenceScorer.Explain(Scores(), "es");

        lines.Should().HaveCount(5);
        lines[0].Should().StartWith("amount:");
        lines[1].Should().StartWith("direction:");
        lines[2].Should().StartWith("merchant:");
        lines[3].Should().StartWith("date:");
        lines[4].Should().StartWith("currency:");
    }

    [Fact]
    public void Explain_SpanishCopy_IsUsedForSpanish()
    {
        var lines = ConfidenceScorer.Explain(Scores(), "es");

        lines.Should().ContainSingle(l => l.Contains("importe extraído y normalizado"));
        lines.Should().ContainSingle(l => l.Contains("comercio extraído del mensaje"));
    }

    [Fact]
    public void Explain_EnglishCopy_IsUsedForEnglish()
    {
        var lines = ConfidenceScorer.Explain(Scores(), "en");

        lines.Should().ContainSingle(l => l.Contains("amount extracted and normalized"));
        lines.Should().ContainSingle(l => l.Contains("merchant extracted from the message"));
    }

    [Theory]
    [InlineData("es")]
    [InlineData("en")]
    public void Explain_ScoresAreFormattedWithTwoDecimalsInBothLanguages(string language)
    {
        ConfidenceScorer.Explain(Scores(), language).Should().OnlyContain(l => l.Contains(": 1.00 — "));
    }

    [Theory]
    [InlineData(1.0, "amount extracted", "importe extraído")]
    [InlineData(0.5, "two amount-pattern matches", "dos coincidencias")]
    [InlineData(0.3, "three or more amount-pattern matches", "tres o más coincidencias")]
    [InlineData(0.0, "not a valid amount", "no es un importe válido")]
    public void Explain_AmountScorePicksTheRightBand(double score, string englishMarker, string spanishMarker)
    {
        ConfidenceScorer.Explain(Scores(amount: (decimal)score), "en").First()
            .Should().Contain(englishMarker);
        ConfidenceScorer.Explain(Scores(amount: (decimal)score), "es").First()
            .Should().Contain(spanishMarker);
    }

    [Fact]
    public void Explain_AmbiguousDirection_ExplainsWhyTheRuleTypeWasKept()
    {
        ConfidenceScorer.Explain(Scores(direction: 0m), "en").ElementAt(1)
            .Should().Contain("rule type kept");
        ConfidenceScorer.Explain(Scores(direction: 0m), "es").ElementAt(1)
            .Should().Contain("se mantiene el tipo de la regla");
    }

    [Theory]
    [InlineData(1.0, "merchant extracted")]
    [InlineData(0.3, "boilerplate")]
    [InlineData(0.0, "no merchant captured")]
    public void Explain_MerchantScorePicksTheRightBand(double score, string englishMarker)
    {
        ConfidenceScorer.Explain(Scores(merchant: (decimal)score), "en").ElementAt(2)
            .Should().Contain(englishMarker);
    }

    [Theory]
    [InlineData(1.0, "not in the future")]
    [InlineData(0.5, "receipt date")]
    [InlineData(0.0, "unparseable or in the future")]
    public void Explain_DateScorePicksTheRightBand(double score, string englishMarker)
    {
        ConfidenceScorer.Explain(Scores(date: (decimal)score), "en").ElementAt(3)
            .Should().Contain(englishMarker);
    }

    [Fact]
    public void Explain_ContradictingSymbol_ExplainsTheCurrencyMismatch()
    {
        ConfidenceScorer.Explain(Scores(currency: 0m), "en").ElementAt(4)
            .Should().Contain("contradicts the rule currency");
        ConfidenceScorer.Explain(Scores(currency: 0m), "es").ElementAt(4)
            .Should().Contain("contradice la moneda de la regla");
    }

    [Fact]
    public void Explain_MissingFieldKey_SaysItWasNotScored()
    {
        var partial = new Dictionary<string, decimal>
        {
            [ConfidenceScorer.AmountField] = 1m
        };

        var lines = ConfidenceScorer.Explain(partial, "en");

        lines.Should().HaveCount(5);
        lines[2].Should().Contain("field not scored");
        ConfidenceScorer.Explain(partial, "es")[2].Should().Contain("campo sin puntuar");
    }

    [Fact]
    public void Explain_OutOfRangeScore_IsClampedBeforeFormatting()
    {
        ConfidenceScorer.Explain(Scores(amount: 7m), "en").First().Should().Contain(": 1.00 — ");
    }

    [Fact]
    public void Explain_NullDictionary_Throws()
    {
        var act = () => ConfidenceScorer.Explain(null!, "es");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Explain_IsIndependentOfTheAmbientCulture()
    {
        // The reason string must not change with the thread culture; the score formatting is
        // pinned to InvariantCulture precisely so "0.50" never becomes "0,50".
        var original = System.Globalization.CultureInfo.CurrentUICulture;
        try
        {
            System.Globalization.CultureInfo.CurrentUICulture =
                new System.Globalization.CultureInfo("es-ES");

            ConfidenceScorer.Explain(Scores(amount: 0.5m), "en").First()
                .Should().Contain(": 0.50 — ");
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentUICulture = original;
        }
    }
}
