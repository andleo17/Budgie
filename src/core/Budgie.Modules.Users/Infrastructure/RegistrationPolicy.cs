using Budgie.Modules.Users.Application;
using Budgie.Modules.Users.Application.Register;
using Microsoft.Extensions.Configuration;

namespace Budgie.Modules.Users.Infrastructure;

internal sealed class RegistrationPolicy(
    IConfiguration configuration,
    IUserRepository userRepository) : IRegistrationPolicy
{
    public async Task<RegistrationStatusResponse> GetStatusAsync(CancellationToken ct)
    {
        var mode = configuration["Registration:Mode"]?.Trim().ToLowerInvariant();
        var hasUsers = await userRepository.AnyAsync(ct);

        var enabled = mode switch
        {
            "open" => true,
            "closed" => false,
            _ => !hasUsers,
        };

        return new RegistrationStatusResponse(enabled, hasUsers);
    }

    public async Task<bool> CanRegisterAsync(CancellationToken ct)
        => (await GetStatusAsync(ct)).Enabled;
}
