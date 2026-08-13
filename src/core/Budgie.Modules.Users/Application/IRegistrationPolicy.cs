using Budgie.Modules.Users.Application.Register;

namespace Budgie.Modules.Users.Application;

public interface IRegistrationPolicy
{
    Task<RegistrationStatusResponse> GetStatusAsync(CancellationToken ct);
    Task<bool> CanRegisterAsync(CancellationToken ct);
}
