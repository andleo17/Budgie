using Budgie.Modules.Finances.Application.Import;
using Budgie.Modules.Finances.Infrastructure.Import;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Application.Import;

public class MoneyManagerSqliteParserTests
{
    private readonly ILogger<MoneyManagerSqliteParser> _logger = Substitute.For<ILogger<MoneyManagerSqliteParser>>();
    private readonly MoneyManagerSqliteParser _parser;

    public MoneyManagerSqliteParserTests()
    {
        _parser = new MoneyManagerSqliteParser(_logger);
    }

    [Fact]
    public async Task ParseAsync_ValidBackup_ReturnsDataWithCounts()
    {
        var dir = TestData.CreateTempDir();
        try
        {
            var path = TestData.CreateValidBackup(dir);

            var data = await _parser.ParseAsync(path, CancellationToken.None);

            data.Currencies.Should().HaveCount(1);
            data.AssetGroups.Should().HaveCount(1);
            data.Assets.Should().HaveCount(1);
            data.Categories.Should().HaveCount(1);
            data.Transactions.Should().HaveCount(1);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task ParseAsync_EmptyPath_ThrowsInvalidOperation()
    {
        var act = async () => await _parser.ParseAsync("", CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ParseAsync_FileNotFound_ThrowsInvalidOperation()
    {
        var dir = TestData.CreateTempDir();
        try
        {
            var act = async () => await _parser.ParseAsync(Path.Combine(dir, "nope.mmbak"), CancellationToken.None);
            await act.Should().ThrowAsync<InvalidOperationException>();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task ParseAsync_EmptyFile_ThrowsInvalidOperation()
    {
        var dir = TestData.CreateTempDir();
        try
        {
            var path = TestData.WriteFile(dir, "empty.mmbak", []);
            var act = async () => await _parser.ParseAsync(path, CancellationToken.None);
            await act.Should().ThrowAsync<InvalidOperationException>();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task ParseAsync_InvalidMagic_ThrowsInvalidOperation()
    {
        var dir = TestData.CreateTempDir();
        try
        {
            var notSqlite = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
            var path = TestData.WriteFile(dir, "bad.mmbak", notSqlite);
            var act = async () => await _parser.ParseAsync(path, CancellationToken.None);
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*missing 'SQLite format 3' header*");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task ParseAsync_ShortHeader_ThrowsInvalidOperation()
    {
        var dir = TestData.CreateTempDir();
        try
        {
            var path = TestData.WriteFile(dir, "short.mmbak", TestData.SqliteMagic[..5]);
            var act = async () => await _parser.ParseAsync(path, CancellationToken.None);
            await act.Should().ThrowAsync<InvalidOperationException>();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task ParseAsync_Over10Mb_ThrowsInvalidOperation()
    {
        var dir = TestData.CreateTempDir();
        try
        {
            var bytes = new byte[10 * 1024 * 1024 + 1];
            Array.Copy(TestData.SqliteMagic, bytes, TestData.SqliteMagic.Length);
            var path = TestData.WriteFile(dir, "big.mmbak", bytes);
            var act = async () => await _parser.ParseAsync(path, CancellationToken.None);
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*exceeds maximum size*");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task ParseAsync_MissingRequiredTable_ThrowsInvalidOperation()
    {
        var dir = TestData.CreateTempDir();
        try
        {
            var path = TestData.CreateBackupMissingTable(dir);
            var act = async () => await _parser.ParseAsync(path, CancellationToken.None);
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*required table*");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task ParseAsync_Stream_Null_ThrowsInvalidOperation()
    {
        Stream? stream = null;
        var act = async () => await _parser.ParseAsync(stream!, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ParseAsync_Stream_Valid_ReturnsData()
    {
        var dir = TestData.CreateTempDir();
        try
        {
            var path = TestData.CreateValidBackup(dir);
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);

            var data = await _parser.ParseAsync(stream, CancellationToken.None);

            data.Transactions.Should().HaveCount(1);
            data.Assets.Should().HaveCount(1);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
