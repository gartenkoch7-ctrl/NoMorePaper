using NoMorePaper.Application.Classification;

namespace NoMorePaper.Server.Inputs;

/// <summary>
/// Log events of preferences, input state, owner check and input set-up (Server 1400–1499,
/// docs/kb/telemetrie.md). Account and document ids, counts and fixed words only: never addresses,
/// hosts, user names or passwords.
/// </summary>
internal static partial class InputsLog
{
    [LoggerMessage(EventId = 1410, Level = LogLevel.Information, Message = "Account {AccountId} saved its preferences ({SenderCount} mail senders)")]
    public static partial void PreferencesSaved(this ILogger logger, Guid accountId, int senderCount);

    [LoggerMessage(EventId = 1411, Level = LogLevel.Warning, Message = "Preferences of account {AccountId} refused: {Reason}")]
    public static partial void PreferencesRefused(this ILogger logger, Guid accountId, string reason);

    [LoggerMessage(EventId = 1420, Level = LogLevel.Information, Message = "Owner check {Action} on document {DocumentId}: {Outcome}")]
    public static partial void OwnerCheckAnswered(this ILogger logger, string action, Guid documentId, ChangeOutcome outcome);

    [LoggerMessage(EventId = 1421, Level = LogLevel.Warning, Message = "Hand-over of document {DocumentId} refused: the request names no account")]
    public static partial void HandOverWithoutAccount(this ILogger logger, Guid documentId);
}
