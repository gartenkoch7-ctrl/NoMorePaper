using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Editing;
using NoMorePaper.Application.Recognition;
using NoMorePaper.Application.Separation;

namespace NoMorePaper.Infrastructure.Recognition;

/// <summary>
/// <see cref="ISeparationAssessor"/> over the same Ollama as recognition (<c>/api/chat</c> with
/// <see cref="SeparationSchema"/>), through the <see cref="LlmGate"/> with the priority of recognition: the
/// separation stage holds a job's lock like recognition does. The same model and context size as
/// recognition, so Ollama never reloads the model between the two (ADR-0024).
/// </summary>
/// <remarks>Owns its <see cref="HttpClient"/>, whose own time limit is off: each call sets its own.</remarks>
public sealed class OllamaSeparationAssessor(
    HttpClient http, OllamaSettings settings, LlmGate gate, TimeProvider time, ILogger<OllamaSeparationAssessor> logger)
    : ISeparationAssessor, IDisposable
{
    /// <summary>One sender of up to a few words per page and the boundaries; the cap stops a runaway generation.</summary>
    public const int AnswerTokens = 600;

    public async Task<SeparationAssessment> AssessAsync(IReadOnlyList<PageInfo> pages, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pages);
        var asked = pages.OrderBy(page => page.Page).Take(SeparationPrompt.MaxPages).ToList();
        if (pages.Count > asked.Count)
        {
            logger.StackCut(pages.Count, SeparationPrompt.MaxPages);
        }

        // Waiting for other requests must end while the job's lock still covers a whole request.
        using var lease = await gate.EnterAsync(LlmPriority.Recognition, OllamaRecognizer.MaxWait(settings), cancellationToken).ConfigureAwait(false);
        var started = time.GetTimestamp();
        var content = await AskAsync(Request(asked), cancellationToken).ConfigureAwait(false);
        var assessment = SeparationAnswer.Parse(content, asked.Count, out var dropped);
        if (dropped > 0)
        {
            logger.BoundariesDropped(dropped);
        }

        var elapsedMs = (long)time.GetElapsedTime(started).TotalMilliseconds;
        logger.StackAssessed(asked.Count, elapsedMs, assessment.Boundaries.Count);
        return assessment;
    }

    public void Dispose() => http.Dispose();

    private async Task<string> AskAsync(JsonObject body, CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(settings.Timeout, time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        var started = time.GetTimestamp();
        try
        {
            using var response = await http.PostAsJsonAsync("api/chat", body, linked.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger.ServiceRefused((int)response.StatusCode);
                throw new RecognitionException($"LLM service answered {(int)response.StatusCode}");
            }

            using var json = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(linked.Token).ConfigureAwait(false), cancellationToken: linked.Token).ConfigureAwait(false);
            var root = json.RootElement;
            OllamaLog.RequestDuration.Record(time.GetElapsedTime(started).TotalSeconds);
            var reason = root.TryGetProperty("done_reason", out var done) && done.ValueKind == JsonValueKind.String ? done.GetString() : null;
            if (reason != "stop"
                || !root.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object
                || !message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.String)
            {
                logger.AnswerUnusable(reason == "length" ? "done-reason-length" : "no-content");
                throw new InvalidDataException("The answer of the model is incomplete.");
            }

            return content.GetString() ?? string.Empty;
        }
        catch (HttpRequestException exception)
        {
            // The message may name the host; the type of the cause is enough.
            logger.ServiceUnreachable(exception.InnerException?.GetType().Name ?? exception.GetType().Name);
            throw new RecognitionException("LLM service not reachable", exception);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            logger.TimedOut(settings.Timeout.TotalSeconds);
            throw new TimeoutException($"The model did not answer within {settings.Timeout.TotalSeconds} s.");
        }
        catch (JsonException exception)
        {
            logger.AnswerUnusable("invalid-response");
            throw new InvalidDataException("LLM service answer is no JSON", exception);
        }
    }

    private JsonObject Request(IReadOnlyList<PageInfo> pages)
    {
        var options = new JsonObject
        {
            ["temperature"] = 0,
            ["seed"] = 1,

            // Like recognition: another context size would make Ollama load the model again.
            ["num_ctx"] = OllamaRecognizer.ContextTokens,
            ["num_predict"] = AnswerTokens,
        };
        if (settings.NumThread > 0)
        {
            options["num_thread"] = settings.NumThread;
        }

        return new JsonObject
        {
            ["model"] = settings.Model,
            ["stream"] = false,
            ["keep_alive"] = OllamaRecognizer.KeepAlive,
            ["format"] = SeparationSchema.Create(),
            ["options"] = options,
            ["messages"] = new JsonArray(
                new JsonObject { ["role"] = "system", ["content"] = SeparationPrompt.System },
                new JsonObject { ["role"] = "user", ["content"] = SeparationPrompt.User(pages) }),
        };
    }
}
