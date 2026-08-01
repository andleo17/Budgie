using Budgie.Modules.Finances.Application.Accounts.GetAccountById;
using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Application.Accounts.GetAccountById;

public class GetAccountByIdQueryHandlerTests
{
    private readonly FinancesDbContext _dbContext;
    private readonly GetAccountByIdQueryHandler _handler;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly Guid _userId = Guid.NewGuid();

    public GetAccountByIdQueryHandlerTests()
    {
        _currentUserProvider = Substitute.For<ICurrentUserProvider>();
        _currentUserProvider.UserId.Returns(_userId);

        var options = new DbContextOptionsBuilder<FinancesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new FinancesDbContext(options, _currentUserProvider);
        _handler = new GetAccountByIdQueryHandler(_dbContext);
    }

    [Fact]
    public async Task Handle_ShouldReturnAccountById()
    {
        var account = Account.Create(_userId, "Wallet", "USD", 500m);
        _dbContext.Accounts.Add(account);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetAccountByIdQuery(account.Id), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Id.Should().Be(account.Id);
        result.Value.Name.Should().Be("Wallet");
        result.Value.Balance.Should().Be(500m);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenAccountDoesNotExist()
    {
        var result = await _handler.Handle(new GetAccountByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorOr.ErrorType.NotFound);
        result.FirstError.Code.Should().Be("Account.NotFound");
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenAccountBelongsToAnotherUser()
    {
        var otherUserId = Guid.NewGuid();
        var account = Account.Create(otherUserId, "Theirs", "USD", 100m);
        _dbContext.Accounts.Add(account);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.Handle(new GetAccountByIdQuery(account.Id), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorOr.ErrorType.NotFound);
    }
}
