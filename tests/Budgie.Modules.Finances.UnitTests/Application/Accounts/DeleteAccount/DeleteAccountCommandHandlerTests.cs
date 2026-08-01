using Budgie.Modules.Finances.Application.Accounts.DeleteAccount;
using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Shared.Enums;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Application.Accounts.DeleteAccount;

public class DeleteAccountCommandHandlerTests
{
    private readonly FinancesDbContext _dbContext;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly DeleteAccountCommandHandler _handler;
    private readonly Guid _userId = Guid.NewGuid();

    public DeleteAccountCommandHandlerTests()
    {
        _currentUserProvider = Substitute.For<ICurrentUserProvider>();
        _currentUserProvider.UserId.Returns(_userId);

        var options = new DbContextOptionsBuilder<FinancesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new FinancesDbContext(options, _currentUserProvider);
        _handler = new DeleteAccountCommandHandler(_dbContext, NullLogger<DeleteAccountCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_WhenAccountExists_ShouldDeleteAccount()
    {
        // Arrange
        var account = Account.Create(_userId, "Test Account", "USD", 0, AccountType.Checking);
        _dbContext.Accounts.Add(account);
        await _dbContext.SaveChangesAsync();

        var command = new DeleteAccountCommand(account.Id);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeFalse();
        var exists = await _dbContext.Accounts.AnyAsync(a => a.Id == account.Id);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenAccountDoesNotExist_ShouldReturnNotFound()
    {
        // Arrange
        var command = new DeleteAccountCommand(Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorOr.ErrorType.NotFound);
    }
}
