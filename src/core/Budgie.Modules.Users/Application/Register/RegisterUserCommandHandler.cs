using Budgie.Modules.Users.Domain;
using Budgie.Modules.Users.Infrastructure.Database;
using Budgie.Modules.Users.Infrastructure.Logging;
using ErrorOr;
using Mediator;
using Microsoft.EntityFrameworkCore;
using BC = BCrypt.Net.BCrypt;

namespace Budgie.Modules.Users.Application.Register;

internal sealed class RegisterUserCommandHandler(
    UsersDbContext dbContext,
    ILogger<RegisterUserCommandHandler> logger)
    : IRequestHandler<RegisterUserCommand, ErrorOr<Guid>>
{
    public async ValueTask<ErrorOr<Guid>> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        if (await dbContext.Users.AnyAsync(u => u.Email == request.Email, cancellationToken))
        {
            return Error.Conflict("User.EmailAlreadyExists", "Email is already in use.");
        }

        var user = User.Create(request.Email, BC.HashPassword(request.Password), request.Name);
        var settings = UserSettings.Create(user.Id);

        dbContext.Users.Add(user);
        dbContext.UserSettings.Add(settings);
        await dbContext.SaveChangesAsync(cancellationToken);

        LogMessages.UserRegistered(logger, request.Email);

        return user.Id;
    }
}
