using Budgie.Modules.Finances.Domain.Import;
using FluentAssertions;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Domain.Import;

public class ImportRunTests
{
    private static readonly string ValidHash = new string('a', 64); // 64 lowercase hex chars

    [Fact]
    public void Create_ValidArguments_ReturnsRun()
    {
        var userId = Guid.NewGuid();
        var run = ImportRun.Create(userId, ValidHash, "backup.mmbak", "{}");

        run.Id.Should().NotBeEmpty();
        run.UserId.Should().Be(userId);
        run.FileHash.Should().Be(ValidHash);
        run.FileName.Should().Be("backup.mmbak");
        run.CountsJson.Should().Be("{}");
        run.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_EmptyUserId_ThrowsArgumentException()
    {
        var act = () => ImportRun.Create(Guid.Empty, ValidHash, null, null);
        act.Should().Throw<ArgumentException>().WithParameterName("userId");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyFileHash_ThrowsArgumentException(string? fileHash)
    {
        var act = () => ImportRun.Create(Guid.NewGuid(), fileHash!, null, null);
        act.Should().Throw<ArgumentException>().WithParameterName("fileHash");
    }

    [Fact]
    public void Create_ShortFileHash_ThrowsArgumentException()
    {
        var act = () => ImportRun.Create(Guid.NewGuid(), "abc", null, null);
        act.Should().Throw<ArgumentException>().WithParameterName("fileHash");
    }

    [Fact]
    public void Create_UppercaseHash_IsLowercasedAndTrimmed()
    {
        var hash = new string('A', 64);
        var run = ImportRun.Create(Guid.NewGuid(), $"  {hash}  ", null, null);
        run.FileHash.Should().Be(new string('a', 64));
    }

    [Fact]
    public void Create_BlankFileName_IsStoredAsNull()
    {
        var run = ImportRun.Create(Guid.NewGuid(), ValidHash, "   ", "{}");
        run.FileName.Should().BeNull();
    }

    [Fact]
    public void Create_BlankCountsJson_IsStoredAsNull()
    {
        var run = ImportRun.Create(Guid.NewGuid(), ValidHash, "x.mmbak", "  ");
        run.CountsJson.Should().BeNull();
    }
}
