namespace Budgie.Shared.Abstractions;

public interface ICurrentUserProvider
{
    Guid? UserId { get; }
    Guid GetUserId();
    bool IsAuthenticated { get; }
}
