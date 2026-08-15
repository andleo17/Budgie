using Budgie.Modules.Users.Application;
using Budgie.Modules.Users.Domain;
using Budgie.Modules.Users.Domain.Events;
using Mediator;

namespace Budgie.Modules.Users.Application.Register.EventHandlers;

internal sealed class CreateDefaultSettingsOnUserRegistered(
    IUserSettingsRepository settingsRepository)
    : INotificationHandler<UserRegisteredEvent>
{
    public async ValueTask Handle(UserRegisteredEvent notification, CancellationToken ct)
    {
        var settings = UserSettings.Create(notification.UserId);
        await settingsRepository.AddAsync(settings, ct);
    }
}
