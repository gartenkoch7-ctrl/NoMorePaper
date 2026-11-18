using Microsoft.Extensions.Logging;

namespace NoMorePaper.Web.Client.Accounts;

/// <summary>
/// Log events of the account pages (EventId range 4400–4499, see docs/kb/telemetrie.md).
/// Never logs user names, display names, passwords, emergency codes or link tokens: only
/// operation names, flags and HTTP status codes.
/// </summary>
internal static partial class AccountsLog
{
    [LoggerMessage(EventId = 4400, EventName = "SessionLoaded", Level = LogLevel.Information,
        Message = "Session state loaded: {State}")]
    public static partial void SessionLoaded(this ILogger logger, SessionStatus state);

    [LoggerMessage(EventId = 4401, EventName = "SessionExpired", Level = LogLevel.Information,
        Message = "The server answered 401, the session is over")]
    public static partial void SessionExpired(this ILogger logger);

    [LoggerMessage(EventId = 4402, EventName = "SignedIn", Level = LogLevel.Information,
        Message = "Signed in")]
    public static partial void SignedIn(this ILogger logger);

    [LoggerMessage(EventId = 4403, EventName = "SignedOut", Level = LogLevel.Information,
        Message = "Signed out, everywhere: {Everywhere}")]
    public static partial void SignedOut(this ILogger logger, bool everywhere);

    [LoggerMessage(EventId = 4404, EventName = "AccountApiFailed", Level = LogLevel.Warning,
        Message = "Account call {Operation} failed with HTTP status {StatusCode}")]
    public static partial void AccountApiFailed(this ILogger logger, string operation, int statusCode);

    [LoggerMessage(EventId = 4405, EventName = "AccountApiUnreachable", Level = LogLevel.Warning,
        Message = "Account call {Operation} failed: {ExceptionType}")]
    public static partial void AccountApiUnreachable(this ILogger logger, string operation, string exceptionType);

    [LoggerMessage(EventId = 4406, EventName = "HouseholdSetUp", Level = LogLevel.Information,
        Message = "Household set up")]
    public static partial void HouseholdSetUp(this ILogger logger);

    [LoggerMessage(EventId = 4407, EventName = "InvitationCreated", Level = LogLevel.Information,
        Message = "Invitation link created")]
    public static partial void InvitationCreated(this ILogger logger);

    [LoggerMessage(EventId = 4408, EventName = "InvitationAccepted", Level = LogLevel.Information,
        Message = "Invitation accepted")]
    public static partial void InvitationAccepted(this ILogger logger);

    [LoggerMessage(EventId = 4409, EventName = "ResetLinkCreated", Level = LogLevel.Information,
        Message = "Password reset link created")]
    public static partial void ResetLinkCreated(this ILogger logger);

    [LoggerMessage(EventId = 4410, EventName = "MemberRemoved", Level = LogLevel.Information,
        Message = "Member removed")]
    public static partial void MemberRemoved(this ILogger logger);

    [LoggerMessage(EventId = 4411, EventName = "PasswordChanged", Level = LogLevel.Information,
        Message = "Password changed ({Route})")]
    public static partial void PasswordChanged(this ILogger logger, string route);

    [LoggerMessage(EventId = 4412, EventName = "SignInRedirect", Level = LogLevel.Debug,
        Message = "Not signed in, redirecting to the sign-in page")]
    public static partial void SignInRedirect(this ILogger logger);

    [LoggerMessage(EventId = 4413, EventName = "CopyFailed", Level = LogLevel.Warning,
        Message = "Copying to the clipboard failed: {ExceptionType}")]
    public static partial void CopyFailed(this ILogger logger, string exceptionType);
}
