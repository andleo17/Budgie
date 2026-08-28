using System.Globalization;
using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Application.Import;
using Budgie.Modules.Finances.Application.Import.CommitMoneyManagerImport;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Application.Import.CommitMoneyManagerImport;

// El happy-path transaccional (CommitAsync) abre una conexión Npgsql real
// (NpgsqlConnection es sealed, no sustituible) y se cubre por integration tests
// con PostgreSQL. Aquí se cubren todas las ramas previas a abrir la conexión:
// validación de archivo, idempotencia por hash, fallo del parser y límites.
public class CommitMoneyManagerImportCommandHandlerTests
{
    private readonly IMoneyManagerParser _parser = Substitute.For<IMoneyManagerParser>();
    private readonly IAccountRepository _accountRepo = Substitute.For<IAccountRepository>();
    private readonly ICategoryRepository _categoryRepo = Substitute.For<ICategoryRepository>();
    private readonly ITransactionRepository _txRepo = Substitute.For<ITransactionRepository>();
    private readonly IRecurringTransactionRepository _recurringRepo = Substitute.For<IRecurringTransactionRepository>();
    private readonly IImportRunRepository _importRunRepo = Substitute.For<IImportRunRepository>();
    private readonly ICurrentUserProvider _currentUserProvider = Substitute.For<ICurrentUserProvider>();
    private readonly IDbConnectionFactory _factory = Substitute.For<IDbConnectionFactory>();
    private readonly ILogger<CommitMoneyManagerImportCommandHandler> _logger =
        Substitute.For<ILogger<CommitMoneyManagerImportCommandHandler>>();
    private readonly CommitMoneyManagerImportCommandHandler _handler;
    private readonly Guid _userId = Guid.NewGuid();

    public CommitMoneyManagerImportCommandHandlerTests()
    {
        _handler = new CommitMoneyManagerImportCommandHandler(
            _parser, _accountRepo, _categoryRepo, _txRepo, _recurringRepo,
            _importRunRepo, _currentUserProvider, _factory, _logger);
        _currentUserProvider.GetUserId().Returns(_userId);
    }

    private static Stream StreamOf(int sizeBytes = 1024)
    {
        var ms = new MemoryStream();
        ms.SetLength(sizeBytes);
        ms.Position = 0;
        return ms;
    }

    private static MmImportData SmallData() => new(
        Currencies: [new MmCurrency("PEN", 1.0m, true)],
        AssetGroups: [new MmAssetGroup(1, "Cuentas")],
        Assets: [new MmAsset("acc1", "1", 1, "PEN_PEN", "Cuenta", 0, "1735689600000")],
        Categories: [new MmCategory("cat1", "Ingresos", 0, null, 0, 0)],
        Transactions: [new MmTransaction("tx1", "acc1", "-4", "-4", "1735689600000", "2025-01-01", "100", "100", "100", "PEN_PEN", "Saldo", 7, null)],
        Budgets: [],
        BudgetAmounts: [],
        RepeatTransactions: []);

    // --- Validación previa a DB ---
    [Fact]
    public async Task Handle_FileTooLarge_ReturnsValidationError()
    {
        var result = await _handler.Handle(
            new CommitMoneyManagerImportCommand(StreamOf(11 * 1024 * 1024), "x.mmbak", "application/octet-stream"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Import.FileTooLarge");
        _factory.DidNotReceive().CreateConnection();
    }

    [Theory]
    [InlineData(".txt")]
    [InlineData(".bin")]
    public async Task Handle_InvalidExtension_ReturnsInvalidFile(string ext)
    {
        var result = await _handler.Handle(
            new CommitMoneyManagerImportCommand(StreamOf(), $"file{ext}", "application/octet-stream"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Import.InvalidFile");
    }

    [Fact]
    public async Task Handle_UnsupportedContentType_ReturnsInvalidFile()
    {
        var result = await _handler.Handle(
            new CommitMoneyManagerImportCommand(StreamOf(), "x.mmbak", "text/html"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Import.InvalidFile");
    }

    // --- Idempotencia por hash ---
    [Fact]
    public async Task Handle_HashAlreadyImported_AndNotOverwrite_ReturnsConflict()
    {
        _importRunRepo.ExistsByHashAsync(_userId, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await _handler.Handle(
            new CommitMoneyManagerImportCommand(StreamOf(), "x.mmbak", "application/octet-stream"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorOr.ErrorType.Conflict);
        result.FirstError.Code.Should().Be("Import.AlreadyImported");
        _factory.DidNotReceive().CreateConnection();
        _accountRepo.DidNotReceiveWithAnyArgs().AddAsync(default!, default, default);
    }

    [Fact]
    public async Task Handle_HashAlreadyImported_WithOverwrite_ProceedsToParse()
    {
        _importRunRepo.ExistsByHashAsync(_userId, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _parser.ParseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<MmImportData>(new InvalidImportFileException("boom")));

        // Con overwrite=true el conflicto se salta; el siguiente fallo posible es el parser
        var result = await _handler.Handle(
            new CommitMoneyManagerImportCommand(StreamOf(), "x.mmbak", "application/octet-stream", Overwrite: true),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Import.InvalidFile");
    }

    // --- Fallo del parser ---
    [Fact]
    public async Task Handle_ParserThrowsInvalidFile_ReturnsValidationError()
    {
        _importRunRepo.ExistsByHashAsync(_userId, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _parser.ParseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<MmImportData>(new InvalidImportFileException("bad magic")));

        var result = await _handler.Handle(
            new CommitMoneyManagerImportCommand(StreamOf(), "x.mmbak", "application/octet-stream"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Import.InvalidFile");
    }

    // --- Límites ---
    [Fact]
    public async Task Handle_TooManyTransactions_ReturnsLimitExceeded()
    {
        _importRunRepo.ExistsByHashAsync(_userId, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        var manyTx = Enumerable.Range(0, 5001)
            .Select(i => new MmTransaction($"t{i}", "acc1", "-4", "-4", null, "2025-01-01", "1", "1", "1", "PEN_PEN", null, 1, null))
            .ToList();
        _parser.ParseAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(SmallData() with { Transactions = manyTx }));

        var result = await _handler.Handle(
            new CommitMoneyManagerImportCommand(StreamOf(), "x.mmbak", "application/octet-stream"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Import.LimitExceeded");
    }

    // --- i18n ---
    [Fact]
    public async Task Handle_AlreadyImported_EnglishCulture_UsesEnglishMessage()
    {
        _importRunRepo.ExistsByHashAsync(_userId, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        CultureInfo.CurrentUICulture = new CultureInfo("en");
        try
        {
            var result = await _handler.Handle(
                new CommitMoneyManagerImportCommand(StreamOf(), "x.mmbak", "application/octet-stream"),
                CancellationToken.None);
            result.FirstError.Description.Should().Contain("already been imported");
        }
        finally
        {
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        }
    }

    [Fact]
    public async Task Handle_AlreadyImported_SpanishCulture_UsesSpanishMessage()
    {
        _importRunRepo.ExistsByHashAsync(_userId, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        CultureInfo.CurrentUICulture = new CultureInfo("es");
        try
        {
            var result = await _handler.Handle(
                new CommitMoneyManagerImportCommand(StreamOf(), "x.mmbak", "application/octet-stream"),
                CancellationToken.None);
            result.FirstError.Description.Should().Contain("ya fue importada");
        }
        finally
        {
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        }
    }
}