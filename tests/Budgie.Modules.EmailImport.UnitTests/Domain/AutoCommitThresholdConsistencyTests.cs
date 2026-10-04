using System.Globalization;
using System.Text.RegularExpressions;
using Budgie.Modules.EmailImport.Application.Parsing;
using Budgie.Modules.EmailImport.Domain.Connections;
using Budgie.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace Budgie.Modules.EmailImport.UnitTests.Domain;

/// <summary>
/// Slice 11 (§6.1): the auto-commit threshold 0.85 exists in three places by necessity — the
/// C# domain constant (single source of truth), the <c>ConfidenceWeights</c> mirror (a
/// compile-time const reference) and the DDL <c>DEFAULT 0.85</c> in
/// <c>003_email_import.sql</c> (DDL cannot reference C#). This test is the mechanism that keeps
/// the DDL copy from drifting when someone adjusts the constant: the spec's "un solo sitio para
/// poder ajustarlo con un test" — the source is one, and the test forces the other copies to
/// follow.
/// </summary>
/// <remarks>
/// The SQL file is located through <c>AppContext.BaseDirectory</c>, NOT through a repo-relative
/// path: the test project copies it to the output directory (a <c>CopyToOutputDirectory</c> item
/// in the unit test csproj), so the test is valid no matter which working directory the runner
/// uses — invoked from the repo root, from anywhere else, or in CI. A relative path would pass
/// locally by accident and fail everywhere else, which is worse than no test at all.
/// </remarks>
public class AutoCommitThresholdConsistencyTests
{
    private const string DdlFileName = "003_email_import.sql";

    [Fact]
    public void Ddl_DefaultAutoCommitMinConfidence_MatchesTheDomainConstant()
    {
        var ddlPath = Path.Combine(AppContext.BaseDirectory, "Sql", DdlFileName);
        File.Exists(ddlPath).Should().BeTrue(
            $"the DDL must be copied next to the test binary (expected at {ddlPath}); " +
            "check the CopyToOutputDirectory item in Budgie.Modules.EmailImport.UnitTests.csproj");

        var ddl = File.ReadAllText(ddlPath);

        var match = Regex.Match(
            ddl,
            // Lazy same-line match: [^,\r\n] would stop at the comma inside DECIMAL(3,2).
            "\"AutoCommitMinConfidence\"[^\\r\\n]*?DEFAULT\\s+([0-9]+(?:\\.[0-9]+)?)");
        match.Success.Should().BeTrue(
            $"{DdlFileName} must declare \"AutoCommitMinConfidence\" ... DEFAULT <value>; " +
            "the column or its DEFAULT disappeared — the test cannot compare what it cannot read.");

        var ddlDefault = decimal.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);

        // The assertion that gives the test its name: if someone changes the C# constant and
        // forgets the DDL (or the other way round), this fails.
        ddlDefault.Should().Be(EmailConnection.DefaultAutoCommitMinConfidence);
    }

    [Fact]
    public void ConfidenceWeights_DefaultAutoCommitThreshold_MirrorsTheDomainConstant()
    {
        // A const referencing a const is compile-time, but this assertion documents the intent
        // and fails loudly if someone re-introduces an independent literal with a different
        // value in ConfidenceWeights.
        ConfidenceWeights.DefaultAutoCommitThreshold
            .Should().Be(EmailConnection.DefaultAutoCommitMinConfidence);
    }

    [Fact]
    public void Create_DefaultThreshold_IsTheDomainConstant()
    {
        // EmailConnection.Create without an explicit threshold must produce the constant —
        // the third leg of the chain (DDL == constant == create default == ConfidenceWeights).
        var connection = EmailConnection.Create(
            Guid.NewGuid(), EmailProvider.Gmail, "usuario@banco.es", "v1:bm9uY2U=:Y2lwaGVy:dGFn");

        connection.AutoCommitMinConfidence.Should().Be(EmailConnection.DefaultAutoCommitMinConfidence);
    }
}
