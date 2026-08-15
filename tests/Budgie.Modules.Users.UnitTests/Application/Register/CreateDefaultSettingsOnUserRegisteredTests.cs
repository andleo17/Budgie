using Budgie.Modules.Users.Application;
using Budgie.Modules.Users.Application.Register.EventHandlers;
using Budgie.Modules.Users.Domain;
using Budgie.Modules.Users.Domain.Events;
using Budgie.Shared.Enums;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Budgie.Modules.Users.UnitTests.Application.Register;

public class CreateDefaultSettingsOnUserRegisteredTests
{
    private readonly IUserSettingsRepository _settingsRepository = Substitute.For<IUserSettingsRepository>();

    [Fact]
    public async Task Handle_CreatesDefaultSettingsForUser()
    {
        var handler = new CreateDefaultSettingsOnUserRegistered(_settingsRepository);
        var notification = new UserRegisteredEvent(Guid.NewGuid(), "test@example.com", "Test User", UserRole.User);

        await handler.Handle(notification, CancellationToken.None);

        await _settingsRepository.Received(1).AddAsync(
            Arg.Is<UserSettings>(s => s.UserId == notification.UserId && !s.IsConfigured),
            Arg.Any<CancellationToken>());
    }
}
