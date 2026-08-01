using Budgie.Modules.Relationships.Application.Tags;

namespace Budgie.Modules.Relationships.Application.Persons;

public record PersonResponse(Guid Id, string Name, List<TagResponse> Tags, string? PhotoUrl = null);
