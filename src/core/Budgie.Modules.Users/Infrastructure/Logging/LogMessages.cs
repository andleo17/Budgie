namespace Budgie.Modules.Users.Infrastructure.Logging;

public static partial class LogMessages
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "User registered: {Email}")]
    public static partial void UserRegistered(ILogger logger, string email);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "User logged in: {Email}")]
    public static partial void UserLoggedIn(ILogger logger, string email);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Failed login attempt for {Email}")]
    public static partial void FailedLoginAttempt(ILogger logger, string email);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Settings updated for user {UserId}")]
    public static partial void SettingsUpdated(ILogger logger, Guid userId);
}
