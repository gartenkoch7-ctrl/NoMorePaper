using NoMorePaper.Application.Inputs.Mail;

namespace NoMorePaper.Server.Inputs;

/// <summary>
/// Log events of the mailbox set-up (Server 1400–1402, docs/kb/telemetrie.md). Account id, fixed words and
/// the security mode only: never host, user name, authserv-id or password.
/// </summary>
internal static partial class MailSettingsLog
{
    [LoggerMessage(EventId = 1400, Level = LogLevel.Information, Message = "Account {AccountId} saved the mailbox set-up ({Security}, password renewed: {Renewed})")]
    public static partial void MailboxSaved(this ILogger logger, Guid accountId, MailboxSecurity security, bool renewed);

    [LoggerMessage(EventId = 1401, Level = LogLevel.Warning, Message = "Mailbox set-up of account {AccountId} refused: {Reason}")]
    public static partial void MailboxRefused(this ILogger logger, Guid accountId, string reason);

    [LoggerMessage(EventId = 1402, Level = LogLevel.Information, Message = "Account {AccountId} tested the mailbox: {Result}")]
    public static partial void MailboxTested(this ILogger logger, Guid accountId, MailboxProbeResult result);
}
