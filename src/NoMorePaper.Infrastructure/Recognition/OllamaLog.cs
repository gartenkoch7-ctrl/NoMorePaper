using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using NoMorePaper.Infrastructure.Telemetry;

namespace NoMorePaper.Infrastructure.Recognition;

/// <summary>
/// Log events and instruments of the Ollama client (Infrastructure 3520–3599, docs/kb/telemetrie.md).
/// Only durations, counts, status codes, the configured model and fixed reasons; never prompt, answer or
/// document content.
/// </summary>
internal static partial class OllamaLog
{
    public static readonly Histogram<double> RequestDuration = InfrastructureTelemetry.Meter.CreateHistogram<double>(
        "nomorepaper.recognition.llm_duration", unit: "s", description: "Duration of one request to the local LLM");

    [LoggerMessage(EventId = 3520, Level = LogLevel.Information, Message = "Model answered after {ElapsedMs} ms ({PromptEvalCount} prompt and {EvalCount} answer tokens, {LoadMs} ms loading)")]
    public static partial void ModelAnswered(this ILogger logger, long elapsedMs, int promptEvalCount, int evalCount, long loadMs);

    [LoggerMessage(EventId = 3521, Level = LogLevel.Warning, Message = "The answer of the model cannot be used ({Reason})")]
    public static partial void AnswerUnusable(this ILogger logger, string reason);

    [LoggerMessage(EventId = 3522, Level = LogLevel.Warning, Message = "The LLM service answered with status {StatusCode}")]
    public static partial void ServiceRefused(this ILogger logger, int statusCode);

    [LoggerMessage(EventId = 3523, Level = LogLevel.Warning, Message = "The LLM service is not reachable ({ErrorType})")]
    public static partial void ServiceUnreachable(this ILogger logger, string errorType);

    [LoggerMessage(EventId = 3524, Level = LogLevel.Warning, Message = "The model did not answer within {TimeoutSeconds} s; the request was cancelled")]
    public static partial void TimedOut(this ILogger logger, double timeoutSeconds);

    [LoggerMessage(EventId = 3525, Level = LogLevel.Information, Message = "Model {Model} is available")]
    public static partial void ModelPresent(this ILogger logger, string model);

    [LoggerMessage(EventId = 3526, Level = LogLevel.Information, Message = "Model {Model} is missing and is being downloaded")]
    public static partial void ModelPulling(this ILogger logger, string model);

    [LoggerMessage(EventId = 3527, Level = LogLevel.Information, Message = "Model {Model} downloaded after {ElapsedMs} ms")]
    public static partial void ModelPulled(this ILogger logger, string model, long elapsedMs);

    [LoggerMessage(EventId = 3528, Level = LogLevel.Warning, Message = "The request filled {PromptEvalCount} of {WindowSize} context tokens; Ollama may have cut its start (rules, lists)")]
    public static partial void ContextFull(this ILogger logger, int promptEvalCount, int windowSize);
}
