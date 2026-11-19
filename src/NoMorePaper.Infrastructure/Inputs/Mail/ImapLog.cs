using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Inputs.Mail;

namespace NoMorePaper.Infrastructure.Inputs.Mail;

/// <summary>
/// Log events of the IMAP mailbox (Infrastructure 3700–3799, docs/kb/telemetrie.md). Durations, counts,
/// folder kinds and exception types only: never host, user name, password, addresses or Message-IDs.
/// </summary>
internal static partial class ImapLog
{
    [LoggerMessage(EventId = 3700, Level = LogLevel.Information, Message = "Signed in to the mailbox after {ElapsedMs} ms")]
    public static partial void SignedIn(this ILogger logger, long elapsedMs);

    [LoggerMessage(EventId = 3701, Level = LogLevel.Warning, Message = "The mail server refused the sign-in")]
    public static partial void SignInRefused(this ILogger logger);

    [LoggerMessage(EventId = 3702, Level = LogLevel.Warning, Message = "The mail server is not reachable ({ExceptionType})")]
    public static partial void Unreachable(this ILogger logger, string exceptionType);

    [LoggerMessage(EventId = 3703, Level = LogLevel.Information, Message = "Created the mailbox folder for {Target}")]
    public static partial void FolderCreated(this ILogger logger, MailFolder target);

    [LoggerMessage(EventId = 3704, Level = LogLevel.Warning, Message = "A mail cannot be read as MIME ({ExceptionType})")]
    public static partial void Unreadable(this ILogger logger, string exceptionType);

    [LoggerMessage(EventId = 3705, Level = LogLevel.Information, Message = "The mailbox is not set up; nothing to fetch")]
    public static partial void NotConfigured(this ILogger logger);

    [LoggerMessage(EventId = 3706, Level = LogLevel.Warning, Message = "The mail server refused a command ({ExceptionType}); the run stops")]
    public static partial void CommandRefused(this ILogger logger, string exceptionType);

    [LoggerMessage(EventId = 3707, Level = LogLevel.Information, Message = "The mail server knows no MOVE; a mail was copied and marked deleted (expunged: {Expunged})")]
    public static partial void MovedByCopy(this ILogger logger, bool expunged);
}
