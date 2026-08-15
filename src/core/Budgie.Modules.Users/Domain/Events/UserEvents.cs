using Budgie.Shared.Domain.Events;
using Budgie.Shared.Enums;

namespace Budgie.Modules.Users.Domain.Events;

public record UserRegisteredEvent(Guid UserId, string Email, string Name, UserRole Role) : IDomainEvent;
