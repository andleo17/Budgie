using Budgie.Modules.Users.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Users.Application.Profile.UpdateProfile;

internal sealed class UpdateProfileCommandHandler(
    UsersDbContext dbContext,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<UpdateProfileCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserProvider.GetUserId();
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
            return Error.NotFound("User.NotFound", "User not found.");

        user.Update(request.Name);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
