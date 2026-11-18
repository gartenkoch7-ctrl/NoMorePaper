namespace NoMorePaper.Server.Accounts;

/// <summary>
/// Log events of sign-in and sessions (Server 1300–1399, docs/kb/telemetrie.md). Account ids only:
/// never user names, passwords, cookies, link tokens or emergency codes.
/// </summary>
internal static partial class AccountsLog
{
    [LoggerMessage(EventId = 1300, Level = LogLevel.Information, Message = "Account {AccountId} signed in")]
    public static partial void SignedIn(this ILogger logger, Guid accountId);

    [LoggerMessage(EventId = 1301, Level = LogLevel.Warning, Message = "Sign-in of account {AccountId} failed: wrong password")]
    public static partial void SignInFailed(this ILogger logger, Guid accountId);

    [LoggerMessage(EventId = 1302, Level = LogLevel.Warning, Message = "Sign-in failed: unknown account")]
    public static partial void SignInUnknown(this ILogger logger);

    [LoggerMessage(EventId = 1303, Level = LogLevel.Warning, Message = "Sign-in of account {AccountId} refused: locked for {RetryAfterSeconds} s")]
    public static partial void SignInLocked(this ILogger logger, Guid accountId, long retryAfterSeconds);

    [LoggerMessage(EventId = 1304, Level = LogLevel.Information, Message = "Account {AccountId} signed out")]
    public static partial void SignedOut(this ILogger logger, Guid accountId);

    [LoggerMessage(EventId = 1305, Level = LogLevel.Information, Message = "Session of account {AccountId} ended: the account was removed or signed out everywhere")]
    public static partial void SessionEnded(this ILogger logger, Guid accountId);

    [LoggerMessage(EventId = 1306, Level = LogLevel.Warning, Message = "Password change of account {AccountId} refused: locked for {RetryAfterSeconds} s")]
    public static partial void PasswordChangeLocked(this ILogger logger, Guid accountId, long retryAfterSeconds);
}
