using Budgie.Modules.Relationships.Domain.Tags;
using Budgie.Modules.Relationships.Infrastructure.Database;
using Budgie.Modules.Relationships.Infrastructure.Logging;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Relationships.Application.Persons.UpdatePerson;

internal sealed class UpdatePersonCommandHandler(
    RelationshipsDbContext dbContext,
    ICurrentUserProvider currentUserProvider,
    ILogger<UpdatePersonCommandHandler> logger)
    : IRequestHandler<UpdatePersonCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(UpdatePersonCommand request, CancellationToken cancellationToken)
    {
        var person = await dbContext.Persons
            .Include(p => p.Tags)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (person is null) return Error.NotFound("Person.NotFound", "Person not found.");

        person.Update(request.Name);

        if (request.TagNames is not null)
        {
            person.Tags.Clear();

            if (request.TagNames.Count > 0)
            {
                var userId = currentUserProvider.GetUserId();
                var existingTags = await dbContext.Tags
                    .Where(t => t.UserId == userId && request.TagNames.Contains(t.Name))
                    .ToListAsync(cancellationToken);

                foreach (var tagName in request.TagNames)
                {
                    var tag = existingTags.FirstOrDefault(t => t.Name == tagName);
                    if (tag is null)
                    {
                        tag = Tag.Create(userId, tagName);
                        dbContext.Tags.Add(tag);
                    }

                    person.Tags.Add(tag);
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        LogMessages.PersonUpdated(logger, request.Id);

        return Result.Success;
    }
}
