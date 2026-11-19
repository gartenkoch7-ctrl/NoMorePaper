using Microsoft.Extensions.Logging;

namespace NoMorePaper.Infrastructure.Inputs.ScannerFolder;

/// <summary>
/// Log events of the scanner folder (Infrastructure 3600–3699, docs/kb/telemetrie.md). Counts, account ids,
/// fixed reasons and exception types only; never a file or folder name (AGENTS.md 2.3).
/// </summary>
internal static partial class ScannerFolderLog
{
    [LoggerMessage(EventId = 3600, Level = LogLevel.Debug, Message = "Scanner folder read: {Count} finished files, {Accepted} accepted")]
    public static partial void Ran(this ILogger logger, int count, int accepted);

    [LoggerMessage(EventId = 3601, Level = LogLevel.Warning, Message = "A file of the scanner folder was moved to the error folder ({Reason})")]
    public static partial void MovedToErrors(this ILogger logger, string reason);

    [LoggerMessage(EventId = 3602, Level = LogLevel.Warning, Message = "A taken file could not be deleted ({ExceptionType}); the next run drops it as a duplicate")]
    public static partial void DeleteFailed(this ILogger logger, string exceptionType);

    [LoggerMessage(EventId = 3603, Level = LogLevel.Information, Message = "Scanner subfolder created for account {AccountId}")]
    public static partial void AccountFolderCreated(this ILogger logger, Guid accountId);

    [LoggerMessage(EventId = 3604, Level = LogLevel.Warning, Message = "{Count} scanner subfolders belong to no account; their files stay where they are")]
    public static partial void UnknownFolders(this ILogger logger, int count);

    [LoggerMessage(EventId = 3605, Level = LogLevel.Warning, Message = "A file of the scanner folder could not be taken in ({ExceptionType}); it stays for the next run")]
    public static partial void IntakeFailed(this ILogger logger, string exceptionType);

    [LoggerMessage(EventId = 3606, Level = LogLevel.Warning, Message = "The scanner folder is missing or cannot be read ({Reason})")]
    public static partial void Unreachable(this ILogger logger, string reason);

    [LoggerMessage(EventId = 3607, Level = LogLevel.Information, Message = "A file changed while it was taken in; it stays and is read again")]
    public static partial void ChangedDuringIntake(this ILogger logger);

    [LoggerMessage(EventId = 3608, Level = LogLevel.Warning, Message = "Scanner subfolder for account {AccountId} could not be created ({ExceptionType})")]
    public static partial void AccountFolderFailed(this ILogger logger, Guid accountId, string exceptionType);

    [LoggerMessage(EventId = 3609, Level = LogLevel.Warning, Message = "A file could not be moved to the error folder ({ExceptionType}); it stays")]
    public static partial void MoveFailed(this ILogger logger, string exceptionType);
}
