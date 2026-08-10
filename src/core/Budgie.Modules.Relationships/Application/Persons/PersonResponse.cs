namespace Budgie.Modules.Relationships.Application.Persons;

public record PersonResponse(Guid Id, string Name, string? PhotoPath, List<TagDto>? Tags);
public record TagDto(Guid Id, string Name, string? Color);
