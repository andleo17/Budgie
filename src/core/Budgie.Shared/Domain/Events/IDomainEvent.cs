using Mediator;

namespace Budgie.Shared.Domain.Events;

/// <summary>
/// Marker interface for in-process Domain Events published via Mediator.
/// </summary>
public interface IDomainEvent : INotification
{
    DateTime OccurredOn => DateTime.UtcNow;
}
