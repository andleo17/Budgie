using Budgie.Modules.Relationships.Domain.Persons;
using Budgie.Modules.Relationships.Domain.Tags;
using Budgie.Modules.Relationships.Infrastructure.Database;
using Budgie.Modules.Relationships.Infrastructure.Logging;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Relationships.Application.Persons.CreatePerson;

internal sealed class CreatePersonCommandHandler(
    RelationshipsDbContext dbContext,
    ICurrentUserProvider currentUserProvider,
    ILogger<CreatePersonCommandHandler> logger)
    : IRequestHandler<CreatePersonCommand, ErrorOr<Guid>>
{
    public async ValueTask<ErrorOr<Guid>> Handle(CreatePersonCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserProvider.GetUserId();
        var person = Person.Create(userId, request.Name);

        if (request.TagNames is { Count: > 0 })
        {
            var existingTags = await dbContext.Tags
                .Where(t => t.UserId == userId && request.TagNames.Contains(t.Name))
                .ToListAsync(cancellationToken);

            foreach (var tagName in request.TagNames)
            {
                var tag = existingTags.FirstOrDefault(t => t.Name == tagName)
                    ?? Tag.Create(userId, tagName);
                person.Tags.Add(tag);
            }
        }

        dbContext.Persons.Add(person);
        await dbContext.SaveChangesAsync(cancellationToken);

        LogMessages.PersonCreated(logger, person.Id, person.Name);

        return person.Id;
    }
}
