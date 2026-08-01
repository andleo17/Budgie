using Budgie.Modules.Relationships.Infrastructure.Database;
using Budgie.Modules.Relationships.Infrastructure.Logging;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Relationships.Application.Persons.DeletePerson;

internal sealed class DeletePersonCommandHandler(
    RelationshipsDbContext dbContext,
    ILogger<DeletePersonCommandHandler> logger)
    : IRequestHandler<DeletePersonCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(DeletePersonCommand request, CancellationToken cancellationToken)
    {
        var person = await dbContext.Persons.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
        if (person is null) return Error.NotFound("Person.NotFound", "Person not found.");

        dbContext.Persons.Remove(person);
        await dbContext.SaveChangesAsync(cancellationToken);

        LogMessages.PersonDeleted(logger, request.Id);

        return Result.Success;
    }
}
