using System.Globalization;
using Budgie.Modules.Finances.Application.Import;
using Budgie.Modules.Finances.Application.Import.PreviewMoneyManagerImport;
using Budgie.Modules.Finances.Infrastructure.Import;
using Budgie.Shared.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Application.Import.PreviewMoneyManagerImport;

public class PreviewMoneyManagerImportCommandHandlerTests
{
    private readonly IMoneyManagerParser _parser = Substitute.For<IMoneyManagerParser>();
    private readonly IImportRunRepository _importRunRepo = Substitute.For<IImportRunRepository>();
    private readonly ICurrentUserProvider _currentUserProvider = Substitute.For<ICurrentUserProvider>();
    private readonly ILogger<PreviewMoneyManagerImportCommandHandler> _logger =
        Substitute.For<ILogger<PreviewMoneyManagerImportCommandHandler>>();
    private readonly PreviewMoneyManagerImportCommandHandler _handler;
    private readonly Guid _userId = Guid.NewGuid();

    public PreviewMoneyManagerImportCommandHandlerTests()
    {
        _handler = new PreviewMoneyManagerImportCommandHandler(_parser, _importRunRepo, _currentUserProvider, _logger);
        _currentUserProvider.GetUserId().Returns(_userId);
    }

    private static Stream StreamOf(int sizeBytes = 1024)
    {
        var ms = new MemoryStream();
        ms.SetLength(sizeBytes);
        ms.Position = 0;
        return ms;
    }

    // --- Validation ---
    [Fact]
    public async Task Handle_FileTooLarge_ReturnsValidationError()
    {
        var result = await _handler.Handle(
            new PreviewMoneyManagerImportCommand(StreamOf(11 * 1024 * 1024), "x.mmbak", "application/octet-stream"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Import.FileTooLarge");
        _importRunRepo.DidNotReceiveWithAnyArgs().AddAsync(default!, default, default);
    }

    [Fact]
    public async Task Handle_EmptyFile_ReturnsValidationError()
    {
        var result = await _handler.Handle(
            new PreviewMoneyManagerImportCommand(StreamOf(0), "x.mmbak", "application/octet-stream"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Import.FileTooLarge");
    }

    [Theory]
    [InlineData(".txt")]
    [InlineData(".exe")]
    [InlineData(".mmbakx")]
    public async Task Handle_InvalidExtension_ReturnsInvalidFile(string ext)
    {
        var result = await _handler.Handle(
            new PreviewMoneyManagerImportCommand(StreamOf(), $"file{ext}", "application/octet-stream"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Import.InvalidFile");
    }

    [Fact]
    public async Task Handle_UnsupportedContentType_ReturnsInvalidFile()
    {
        var result = await _handler.Handle(
            new PreviewMoneyManagerImportCommand(StreamOf(), "x.mmbak", "text/html"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Import.InvalidFile");
    }

    // --- Parser failure ---
    [Fact]
    public async Task Handle_ParserThrowsInvalidFile_ReturnsInvalidFile()
    {
        _parser.ParseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<MmImportData>(new InvalidImportFileException("bad magic")));

        var result = await _handler.Handle(
            new PreviewMoneyManagerImportCommand(StreamOf(), "x.mmbak", "application/octet-stream"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Import.InvalidFile");
    }

    // --- Limits ---
    [Fact]
    public async Task Handle_TooManyTransactions_ReturnsLimitExceeded()
    {
        var data = BuildData();
        var manyTx = Enumerable.Range(0, 5001)
            .Select(i => new MmTransaction($"t{i}", "acc1", "-4", "-4", null, "2025-01-01", "1", "1", "1", "PEN_PEN", null, 1, null))
            .ToList();
        _parser.ParseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(data with { Transactions = manyTx }));

        var result = await _handler.Handle(
            new PreviewMoneyManagerImportCommand(StreamOf(), "x.mmbak", "application/octet-stream"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Import.LimitExceeded");
    }

    // --- Happy path with dedup, orphan, already imported ---
    [Fact]
    public async Task Handle_ValidData_ReturnsPreviewWithCounts()
    {
        var data = BuildData();
        _parser.ParseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(data));
        _importRunRepo.ExistsByHashAsync(_userId, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await _handler.Handle(
            new PreviewMoneyManagerImportCommand(StreamOf(), "x.mmbak", "application/octet-stream"),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        var preview = result.Value;

        // 2 real accounts + 1 placeholder skipped
        preview.Accounts.Should().HaveCount(2);
        preview.SkippedPlaceholders.Should().Be(1);

        // 3 active categories (1 deleted skipped): cat1, cat2, cat3(child)
        preview.Categories.Should().HaveCount(3);

        // income=2 (tx2 income + tx1 initial), expense=2 (tx3, tx6), transfer=1 (tx7), initial=1
        preview.Summary.Income.Should().Be(2);
        preview.Summary.Expense.Should().Be(2);
        preview.Summary.Transfer.Should().Be(1);
        preview.Summary.InitialBalance.Should().Be(1);
        preview.Summary.Total.Should().Be(5);
        preview.Summary.DedupedTransfers.Should().Be(1);

        // orphan account error reported
        preview.Errors.Should().Contain(e => e.Contains("orphanAcc"));

        // hash computed and alreadyImported false
        preview.FileHash.Should().NotBeNullOrWhiteSpace();
        preview.AlreadyImported.Should().BeFalse();
        _importRunRepo.DidNotReceiveWithAnyArgs().AddAsync(default!, default, default);
    }

    [Fact]
    public async Task Handle_AlreadyImported_AddsWarning()
    {
        var data = BuildData();
        _parser.ParseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(data));
        _importRunRepo.ExistsByHashAsync(_userId, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await _handler.Handle(
            new PreviewMoneyManagerImportCommand(StreamOf(), "x.mmbak", "application/octet-stream"),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.AlreadyImported.Should().BeTrue();
        result.Value.Warnings.Should().Contain(w => w.Contains("already been imported"));
    }

    // --- Orphan category warning ---
    [Fact]
    public async Task Handle_OrphanCategoryParent_AddsWarning()
    {
        var data = BuildData();
        // Rebuild categories: a child whose pUid doesn't exist in the active set
        var cats = data.Categories
            .Where(c => c.Uid is "cat1" or "cat2")
            .Select(c => c.Uid == "cat2" ? c with { PUid = "missing-parent" } : c)
            .ToList();
        data = data with { Categories = cats };
        _parser.ParseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(data));
        _importRunRepo.ExistsByHashAsync(_userId, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await _handler.Handle(
            new PreviewMoneyManagerImportCommand(StreamOf(), "x.mmbak", "application/octet-stream"),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Warnings.Should().Contain(w => w.Contains("missing parent"));
    }

    // --- i18n es/en ---
    [Fact]
    public async Task Handle_FileTooLarge_UnderEnglishCulture_UsesEnglishMessage()
    {
        var culture = new CultureInfo("en");
        CultureInfo.CurrentUICulture = culture;
        try
        {
            var result = await _handler.Handle(
                new PreviewMoneyManagerImportCommand(StreamOf(11 * 1024 * 1024), "x.mmbak", "application/octet-stream"),
                CancellationToken.None);
            result.FirstError.Description.Should().Contain("between 1 byte and 10 MB");
        }
        finally
        {
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        }
    }

    [Fact]
    public async Task Handle_FileTooLarge_UnderSpanishCulture_UsesSpanishMessage()
    {
        var culture = new CultureInfo("es");
        CultureInfo.CurrentUICulture = culture;
        try
        {
            var result = await _handler.Handle(
                new PreviewMoneyManagerImportCommand(StreamOf(11 * 1024 * 1024), "x.mmbak", "application/octet-stream"),
                CancellationToken.None);
            result.FirstError.Description.Should().Contain("entre 1 byte y 10 MB");
        }
        finally
        {
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        }
    }

    private static MmImportData BuildData() => new(
        Currencies:
        [
            new MmCurrency("PEN", 1.0m, true),
            new MmCurrency("USD", 3.5665m, false)
        ],
        AssetGroups:
        [
            new MmAssetGroup(1, "Cuentas"),
            new MmAssetGroup(2, "Tarjetas")
        ],
        Assets:
        [
            new MmAsset("acc1", "1", 1, "PEN_PEN", "Cuenta Corriente", 0, "1735689600000"),
            new MmAsset("acc2", "2", 2, "PEN_PEN", "Tarjeta Credito", 0, "1735689600000"),
            new MmAsset("11", "1", 1, "PEN_PEN", "Cuentas", 1, "1735689600000")
        ],
        Categories:
        [
            new MmCategory("cat1", "Ingresos", 0, null, 0, 0),
            new MmCategory("cat2", "Gastos", 1, null, 0, 0),
            new MmCategory("cat3", "Comida", 1, "cat2", 0, 0),
            new MmCategory("catDel", "Borrada", 1, null, 1, 0)
        ],
        Transactions:
        [
            new MmTransaction("tx1", "acc1", "-4", "-4", "1735689600000", "2025-01-01", "1000", "1000", "1000", "PEN_PEN", "Saldo", 7, null),
            new MmTransaction("tx2", "acc1", "-4", "cat1", "1736035200000", "2025-01-05", "500", "500", "500", "PEN_PEN", "Ingreso", 0, null),
            new MmTransaction("tx3", "acc1", "-4", "cat3", "1736121600000", "2025-01-06", "50", "50", "50", "PEN_PEN", "Comida", 1, null),
            new MmTransaction("tx7", "acc1", "acc2", "-4", "1736467200000", "2025-01-10", "200", "200", "200", "PEN_PEN", "Transfer", 3, null),
            new MmTransaction("tx8", "acc2", "acc1", "-4", "1736467200000", "2025-01-10", "200", "200", "200", "PEN_PEN", "Transfer", 4, null),
            new MmTransaction("tx6", "orphanAcc", "-4", "-4", "1736553600000", "2025-01-11", "10", "10", "10", "PEN_PEN", "Orphan", 1, null)
        ],
        Budgets: [new MmBudget("b1", "cat1")],
        BudgetAmounts: [new MmBudgetAmount("b1", 100m)],
        RepeatTransactions:
        [
            new MmRepeatTransaction("rep1", "acc1", null, "cat1", "1000", null, "PEN_PEN", 0, 8, "1767225600000", "Sueldo")
        ]);
}
