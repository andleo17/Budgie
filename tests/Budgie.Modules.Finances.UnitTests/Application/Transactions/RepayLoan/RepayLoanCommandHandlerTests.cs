using Budgie.Modules.Finances.Application.Transactions.RepayLoan;
using Budgie.Modules.Finances.Infrastructure;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Enums;
using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Modules.Finances.Domain.Transactions;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Application.Transactions.RepayLoan;

public class RepayLoanCommandHandlerTests : IDisposable
{
    private readonly FinancesDbContext _dbContext;
    private readonly RepayLoanCommandHandler _handler;
    private readonly ICurrentUserProvider _currentUserProvider = Substitute.For<ICurrentUserProvider>();
    private readonly ILanguageProvider _languageProvider = Substitute.For<ILanguageProvider>();
    private readonly ITranslationService _translationService = Substitute.For<ITranslationService>();
    private readonly Guid _userId = Guid.NewGuid();

    public RepayLoanCommandHandlerTests()
    {
        _currentUserProvider.UserId.Returns(_userId);
        _currentUserProvider.GetUserId().Returns(_userId);
        _languageProvider.LanguageCode.Returns("en");
        _translationService.Translate(Arg.Any<string>(), Arg.Any<string>()).Returns("translated note");

        var options = new DbContextOptionsBuilder<FinancesDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new FinancesDbContext(options, _currentUserProvider);
        _handler = new RepayLoanCommandHandler(_dbContext, _currentUserProvider, _languageProvider, _translationService, NullLogger<RepayLoanCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_ShouldCreateRepaymentAndUpdateLoanStatus()
    {
        // Arrange
        var account = Account.Create(_userId, "Wallet", "USD", 1000m);
        _dbContext.Accounts.Add(account);

        var loan = Transaction.Create(
            _userId,
            account.Id, 
            null, 
            100m, 
            "USD", 
            1.0m, 
            DateTime.UtcNow, 
            "Loan to Bob", 
            TransactionType.Loan);
        
        loan.CreateParticipant(Guid.NewGuid(), 100m, remainingAmount: 100m); // Bob owes 100
        _dbContext.Transactions.Add(loan);
        
        account.ApplyTransaction(Money.FromPositive(100m, CurrencyCode.From("USD")), 1.0m, 1, TransactionType.Loan);
        await _dbContext.SaveChangesAsync();

        var command = new RepayLoanCommand(
            loan.Id, 
            100m, 
            "USD", 
            1.0m, 
            DateTime.UtcNow, 
            "Bob paid back");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        
        var updatedLoan = await _dbContext.Transactions
            .Include(t => t.Participants)
            .FirstAsync(t => t.Id == loan.Id);
        
        updatedLoan.Participants.First().RemainingAmount.Amount.Should().Be(0);
        
        var updatedAccount = await _dbContext.Accounts.FindAsync(account.Id);
        updatedAccount!.Balance.Amount.Should().Be(1000m); // 1000 - 100 (loan) + 100 (repayment)
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenOriginalTransactionMissing()
    {
        var command = new RepayLoanCommand(Guid.NewGuid(), 100m, "USD", 1m, DateTime.UtcNow, "Repay");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Transaction.NotFound");
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenAccountMissing()
    {
        var loan = Transaction.Create(_userId, Guid.NewGuid(), null, 100m, "USD", 1m, DateTime.UtcNow, "Loan", TransactionType.Loan);
        _dbContext.Transactions.Add(loan);
        await _dbContext.SaveChangesAsync();

        var command = new RepayLoanCommand(loan.Id, 50m, "USD", 1m, DateTime.UtcNow, "Partial");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Account.NotFound");
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
