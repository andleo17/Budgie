namespace Budgie.Api.Infrastructure.Logging;

public static partial class LogMessages
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Database migration completed for {DbContext}")]
    public static partial void DatabaseMigrated(ILogger logger, string dbContext);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Database operation failed for {DbContext}: {Error}")]
    public static partial void DatabaseError(ILogger logger, string dbContext, string error);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "An error occurred while migrating the database.")]
    public static partial void DatabaseMigrationError(ILogger logger, Exception ex);
}
