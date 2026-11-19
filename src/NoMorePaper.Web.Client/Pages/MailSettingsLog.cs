using Microsoft.Extensions.Logging;

namespace NoMorePaper.Web.Client.Pages;

/// <summary>
/// Log events of the mail settings page (Web.Client 4720–4729, docs/kb/telemetrie.md): whether a password is
/// stored or was renewed, and whether a test got through; never host, user name or password.
/// </summary>
internal static partial class MailSettingsLog
{
    [LoggerMessage(EventId = 4720, EventName = "MailSettingsLoaded", Level = LogLevel.Information,
        Message = "Mail settings loaded (stored: {Stored})")]
    public static partial void MailSettingsLoaded(this ILogger logger, bool stored);

    [LoggerMessage(EventId = 4721, EventName = "MailSettingsSaved", Level = LogLevel.Information,
        Message = "Mail settings saved (renewed: {Renewed})")]
    public static partial void MailSettingsSaved(this ILogger logger, bool renewed);

    [LoggerMessage(EventId = 4722, EventName = "MailSettingsTested", Level = LogLevel.Information,
        Message = "Mail connection tested (signed in: {Succeeded})")]
    public static partial void MailSettingsTested(this ILogger logger, bool succeeded);
}
