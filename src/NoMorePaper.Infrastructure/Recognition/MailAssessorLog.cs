using Microsoft.Extensions.Logging;

namespace NoMorePaper.Infrastructure.Recognition;

/// <summary>
/// Log events of the mail check by the local LLM (Infrastructure, mail block 3700–3799, docs/kb/telemetrie.md);
/// failures of the request itself use the events of <see cref="OllamaLog"/>. Never the mail, prompt or answer.
/// </summary>
internal static partial class MailAssessorLog
{
    [LoggerMessage(EventId = 3710, Level = LogLevel.Information, Message = "Model checked a mail after {ElapsedMs} ms")]
    public static partial void MailAssessed(this ILogger logger, long elapsedMs);

    [LoggerMessage(EventId = 3711, Level = LogLevel.Information, Message = "The note of a mail check named a date or number that is not in the mail and was dropped")]
    public static partial void NoteDropped(this ILogger logger);
}
