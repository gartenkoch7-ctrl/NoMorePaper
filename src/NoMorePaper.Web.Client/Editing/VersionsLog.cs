using Microsoft.Extensions.Logging;

namespace NoMorePaper.Web.Client.Editing;

/// <summary>
/// Log events of the versions section and the account default (Web.Client 4350–4352, docs/kb/telemetrie.md):
/// the chosen policy, a failed call and a finished edit; never titles or file names.
/// </summary>
internal static partial class VersionsLog
{
    [LoggerMessage(EventId = 4350, EventName = "VersionPolicySaved", Level = LogLevel.Information,
        Message = "Version policy saved: {Policy}")]
    public static partial void VersionPolicySaved(this ILogger logger, string policy);

    [LoggerMessage(EventId = 4351, EventName = "VersionsCallFailed", Level = LogLevel.Warning,
        Message = "Version call failed: {Operation}")]
    public static partial void VersionsCallFailed(this ILogger logger, string operation);

    [LoggerMessage(EventId = 4352, EventName = "EditFinishedDetected", Level = LogLevel.Information,
        Message = "Finished edit detected by polling")]
    public static partial void EditFinishedDetected(this ILogger logger);
}
