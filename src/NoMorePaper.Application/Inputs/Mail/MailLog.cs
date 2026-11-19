using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Telemetry;

namespace NoMorePaper.Application.Inputs.Mail;

/// <summary>
/// Log events and instruments of the mail input (Application 5400–5499, docs/kb/telemetrie.md). Counts,
/// verdicts, declared media types and exception types only: never addresses, subjects, mail text, file
/// names or Message-IDs.
/// </summary>
internal static partial class MailLog
{
    public static readonly Counter<long> Assessed = ApplicationTelemetry.Meter.CreateCounter<long>(
        "nomorepaper.mail.assessed", description: "Mails judged by the local LLM, by verdict (spam, document, no-document)");

    [LoggerMessage(EventId = 5400, Level = LogLevel.Information, Message = "Processed a mail: {Documents} new documents, verdict {Verdict}")]
    public static partial void Processed(this ILogger logger, int documents, string verdict);

    [LoggerMessage(EventId = 5401, Level = LogLevel.Warning, Message = "The LLM check of a mail failed ({ExceptionType})")]
    public static partial void AssessmentFailed(this ILogger logger, string exceptionType);

    [LoggerMessage(EventId = 5402, Level = LogLevel.Information, Message = "A part of a mail ({MediaType}) is no document and was left out")]
    public static partial void PartSkipped(this ILogger logger, string mediaType);

    [LoggerMessage(EventId = 5403, Level = LogLevel.Warning, Message = "Moving a mail to {Target} failed ({ExceptionType}); a later run moves it")]
    public static partial void MoveFailed(this ILogger logger, MailFolder target, string exceptionType);

    [LoggerMessage(EventId = 5404, Level = LogLevel.Warning, Message = "A mail failed {Failures} times and was put aside into the folder for failures")]
    public static partial void PutAside(this ILogger logger, int failures);

    [LoggerMessage(EventId = 5405, Level = LogLevel.Warning, Message = "Taking in a mail failed ({ExceptionType}); failure {Failures} of {MaxFailures}")]
    public static partial void Failed(this ILogger logger, string exceptionType, int failures, int maxFailures);

    [LoggerMessage(EventId = 5406, Level = LogLevel.Warning, Message = "The local LLM is not available; the run stops, {Waiting} mails stay in the inbox untouched")]
    public static partial void LlmUnavailable(this ILogger logger, int waiting);

    [LoggerMessage(EventId = 5407, Level = LogLevel.Information, Message = "The run reached its time budget after {Count} mails; the rest waits for the next run")]
    public static partial void BudgetReached(this ILogger logger, int count);

    [LoggerMessage(EventId = 5408, Level = LogLevel.Information, Message = "Mailbox run: {Count} mails, {Accepted} new documents, {Skipped} without document, {Stuck} put aside, {ElapsedMs} ms")]
    public static partial void Run(this ILogger logger, int count, int accepted, int skipped, int stuck, long elapsedMs);
}
