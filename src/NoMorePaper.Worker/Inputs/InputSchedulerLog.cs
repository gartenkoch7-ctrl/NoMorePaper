using NoMorePaper.Application.Inputs;

namespace NoMorePaper.Worker.Inputs;

/// <summary>
/// Log events of the input scheduler (Worker 2300–2399, docs/kb/telemetrie.md). Kinds, counts, problem
/// kinds, durations and exception types only; never names, addresses or paths.
/// </summary>
internal static partial class InputSchedulerLog
{
    [LoggerMessage(EventId = 2300, Level = LogLevel.Information, Message = "Input {Kind} ran: {Accepted} documents taken in, {Skipped} skipped, after {ElapsedMs} ms")]
    public static partial void InputRan(this ILogger logger, InputKind kind, int accepted, int skipped, long elapsedMs);

    [LoggerMessage(EventId = 2301, Level = LogLevel.Warning, Message = "Input {Kind} reports a problem: {Problem} (needs attention: {NeedsAttention})")]
    public static partial void InputProblem(this ILogger logger, InputKind kind, InputProblemKind problem, bool needsAttention);

    [LoggerMessage(EventId = 2302, Level = LogLevel.Error, Message = "Input {Kind} failed ({ExceptionType}); it runs again at its next time")]
    public static partial void InputFailed(this ILogger logger, InputKind kind, string exceptionType);

    [LoggerMessage(EventId = 2303, Level = LogLevel.Warning, Message = "The state of input {Kind} could not be recorded ({ExceptionType})")]
    public static partial void StatusNotRecorded(this ILogger logger, InputKind kind, string exceptionType);

    [LoggerMessage(EventId = 2304, Level = LogLevel.Information, Message = "Input scheduler started with {Count} inputs, checking every {IntervalSeconds} s")]
    public static partial void SchedulerStarted(this ILogger logger, int count, int intervalSeconds);

    [LoggerMessage(EventId = 2305, Level = LogLevel.Error, Message = "The input scheduler could not learn its inputs ({ExceptionType}); no input runs until the Worker restarts")]
    public static partial void SchedulerFailed(this ILogger logger, string exceptionType);
}
