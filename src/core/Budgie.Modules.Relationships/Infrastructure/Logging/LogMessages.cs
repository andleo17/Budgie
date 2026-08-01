namespace Budgie.Modules.Relationships.Infrastructure.Logging;

public static partial class LogMessages
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Person created: {PersonId} ({PersonName})")]
    public static partial void PersonCreated(ILogger logger, Guid personId, string personName);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Person {PersonId} updated")]
    public static partial void PersonUpdated(ILogger logger, Guid personId);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Person {PersonId} deleted")]
    public static partial void PersonDeleted(ILogger logger, Guid personId);
}
