using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Processing;
using NoMorePaper.Application.Recognition;

namespace NoMorePaper.Infrastructure.Recognition;

/// <summary>
/// <see cref="IRecognizer"/> over Ollama's HTTP API (<c>/api/chat</c> with the JSON schema as <c>format</c>),
/// with the options measured in the spike (docs/kb/erkennung.md, "Anfrage"). The service runs on the
/// compose network only; nothing leaves the own network (ADR-0002).
/// </summary>
/// <remarks>Owns its <see cref="HttpClient"/>, whose own time limit is off: each call sets its own.</remarks>
public sealed class OllamaRecognizer(HttpClient http, OllamaSettings settings, LlmGate gate, TimeProvider time, ILogger<OllamaRecognizer> logger)
    : IRecognizer, IDisposable
{
    /// <summary>Largest measured request with the shortening rule was 2 228 tokens, plus the answer.</summary>
    public const int ContextTokens = 4096;

    /// <summary>The answer is about 150 tokens; the cap stops a runaway generation.</summary>
    public const int AnswerTokens = 400;

    /// <summary>Keeps the model loaded between documents; loading takes 6–8 s and drops the prompt cache.</summary>
    public const string KeepAlive = "30m";

    /// <summary>Asking whether the model is there answers at once; a service that does not is stuck.</summary>
    public static readonly TimeSpan ShowTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Downloading 6 GB; a download that takes longer is stuck and is started again.</summary>
    public static readonly TimeSpan PullTimeout = TimeSpan.FromHours(2);

    private const double NanosecondsPerMillisecond = 1e6;

    public async Task PrepareAsync(CancellationToken cancellationToken)
    {
        var model = new JsonObject { ["model"] = settings.Model };
        using (var show = await SendWithinAsync("api/show", model, ShowTimeout, cancellationToken).ConfigureAwait(false))
        {
            if (show.IsSuccessStatusCode)
            {
                logger.ModelPresent(settings.Model);
                return;
            }

            if (show.StatusCode != HttpStatusCode.NotFound)
            {
                throw Refused(show.StatusCode);
            }
        }

        logger.ModelPulling(settings.Model);
        var started = time.GetTimestamp();
        var pull = new JsonObject { ["model"] = settings.Model, ["stream"] = false };
        using var pulled = await SendWithinAsync("api/pull", pull, PullTimeout, cancellationToken).ConfigureAwait(false);
        if (!pulled.IsSuccessStatusCode)
        {
            throw Refused(pulled.StatusCode);
        }

        var elapsed = time.GetElapsedTime(started);
        logger.ModelPulled(settings.Model, (long)elapsed.TotalMilliseconds);
    }

    public async Task<RecognitionResult> RecognizeAsync(RecognitionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Waiting for mail checks must end while the job's lock still covers a whole request.
        using var lease = await gate.EnterAsync(LlmPriority.Recognition, MaxWait(settings), cancellationToken).ConfigureAwait(false);
        using var timeout = new CancellationTokenSource(settings.Timeout, time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        var started = time.GetTimestamp();
        try
        {
            using var response = await SendAsync("api/chat", ChatRequest(request), linked.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw Refused(response.StatusCode);
            }

            var stream = await response.Content.ReadAsStreamAsync(linked.Token).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                using var json = await JsonDocument.ParseAsync(stream, cancellationToken: linked.Token).ConfigureAwait(false);
                var root = json.RootElement;
                var content = Content(root);
                var elapsed = time.GetElapsedTime(started);
                OllamaLog.RequestDuration.Record(elapsed.TotalSeconds);
                var promptCount = Count(root, "prompt_eval_count");
                var answerCount = Count(root, "eval_count");
                var loadMs = (long)(Count64(root, "load_duration") / NanosecondsPerMillisecond);
                logger.ModelAnswered((long)elapsed.TotalMilliseconds, promptCount, answerCount, loadMs);
                if (promptCount + AnswerTokens > ContextTokens)
                {
                    // Ollama cuts a too long input silently; the answer may lack the rules and lists.
                    logger.ContextFull(promptCount, ContextTokens);
                }

                return OllamaAnswer.Parse(content);
            }
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            logger.TimedOut(settings.Timeout.TotalSeconds);
            throw new TimeoutException($"The model did not answer within {settings.Timeout.TotalSeconds} s.");
        }
        catch (JsonException exception)
        {
            logger.AnswerUnusable("invalid-response");
            throw new RecognitionException("LLM service answer is no JSON", exception);
        }
    }

    public void Dispose() => http.Dispose();

    /// <summary>
    /// How long a recognition waits for the <see cref="LlmGate"/>: the job's lock minus the request's time
    /// limit and a minute to spare (at least 4 min, as <see cref="OllamaSettings.MaxTimeout"/> leaves 5).
    /// </summary>
    public static TimeSpan MaxWait(OllamaSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return ProcessNextDocument.LockFor - settings.Timeout - TimeSpan.FromMinutes(1);
    }

    private JsonObject ChatRequest(RecognitionRequest request)
    {
        var options = new JsonObject
        {
            ["temperature"] = 0,
            ["seed"] = 1,
            ["num_ctx"] = ContextTokens,
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
            ["keep_alive"] = KeepAlive,
            ["format"] = OllamaSchema.Create(),
            ["options"] = options,
            ["messages"] = new JsonArray(
                new JsonObject { ["role"] = "system", ["content"] = RecognitionPrompt.System(request) },
                new JsonObject { ["role"] = "user", ["content"] = RecognitionPrompt.User(request) }),
        };
    }

    // An answer cut off by num_predict or ended otherwise is no complete JSON object.
    private string Content(JsonElement root)
    {
        var reason = root.TryGetProperty("done_reason", out var done) && done.ValueKind == JsonValueKind.String ? done.GetString() : null;
        if (reason != "stop")
        {
            var word = reason == "length" ? "length" : "other";
            logger.AnswerUnusable("done-reason-" + word);
            throw new RecognitionException($"answer ended with done_reason {word}");
        }

        if (root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.Object
            && message.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
        {
            return content.GetString() ?? string.Empty;
        }

        logger.AnswerUnusable("no-content");
        throw new RecognitionException("answer has no content");
    }

    private async Task<HttpResponseMessage> SendAsync(string path, JsonObject body, CancellationToken cancellationToken)
    {
        try
        {
            return await http.PostAsJsonAsync(path, body, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            // The message may name the host; the type of the cause is enough.
            logger.ServiceUnreachable(exception.InnerException?.GetType().Name ?? exception.GetType().Name);
            throw new RecognitionException("LLM service not reachable", exception);
        }
    }

    private async Task<HttpResponseMessage> SendWithinAsync(string path, JsonObject body, TimeSpan limit, CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(limit, time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            return await SendAsync(path, body, linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            logger.TimedOut(limit.TotalSeconds);
            throw new RecognitionException("LLM service did not answer in time", exception);
        }
    }

    private RecognitionException Refused(HttpStatusCode status)
    {
        logger.ServiceRefused((int)status);
        return new RecognitionException($"LLM service answered {(int)status}");
    }

    // Only for the log: a missing or odd value never fails an otherwise good answer.
    private static int Count(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var count) ? count : 0;

    private static long Count64(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var count) ? count : 0;
}
