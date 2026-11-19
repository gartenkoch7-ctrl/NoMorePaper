using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Inputs.Mail;
using NoMorePaper.Application.Recognition;

namespace NoMorePaper.Infrastructure.Recognition;

/// <summary>
/// <see cref="IMailAssessor"/> over the same Ollama as recognition (<c>/api/chat</c> with <see cref="MailSchema"/>),
/// through the <see cref="LlmGate"/> behind every waiting recognition. The same model and context size as
/// recognition, so Ollama never reloads the model between the two (ADR-0024).
/// </summary>
/// <remarks>Owns its <see cref="HttpClient"/>, whose own time limit is off: each call sets its own.</remarks>
public sealed class OllamaMailAssessor(HttpClient http, OllamaSettings settings, LlmGate gate, TimeProvider time, ILogger<OllamaMailAssessor> logger)
    : IMailAssessor, IDisposable
{
    /// <summary>Longest wait for recognitions ahead; then the run ends and the mail waits for the next one.</summary>
    public static readonly TimeSpan MaxWait = TimeSpan.FromMinutes(4);

    /// <summary>The answer is two booleans and at most two sentences.</summary>
    public const int AnswerTokens = 200;

    public async Task<MailAssessment> AssessAsync(MailText mail, bool knownSender, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mail);
        IDisposable lease;
        try
        {
            lease = await gate.EnterAsync(LlmPriority.Mail, MaxWait, cancellationToken).ConfigureAwait(false);
        }
        catch (LlmBusyException exception)
        {
            throw new LlmUnavailableException("The local LLM stayed busy with recognition.", exception);
        }

        using (lease)
        {
            var content = await AskAsync(Request(mail, knownSender), cancellationToken).ConfigureAwait(false);
            var assessment = MailAnswer.Parse(content, knownSender, mail.Subject + "\n" + mail.Body, out var noteDropped);
            if (noteDropped)
            {
                logger.NoteDropped();
            }

            return assessment;
        }
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
                throw new LlmUnavailableException($"LLM service answered {(int)response.StatusCode}");
            }

            using var json = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(linked.Token).ConfigureAwait(false), cancellationToken: linked.Token).ConfigureAwait(false);
            var root = json.RootElement;
            var elapsedMs = (long)time.GetElapsedTime(started).TotalMilliseconds;
            OllamaLog.RequestDuration.Record(elapsedMs / 1000d);
            var reason = root.TryGetProperty("done_reason", out var done) && done.ValueKind == JsonValueKind.String ? done.GetString() : null;
            if (reason != "stop"
                || !root.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object
                || !message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.String)
            {
                logger.AnswerUnusable(reason == "length" ? "done-reason-length" : "no-content");
                throw new InvalidDataException("The answer of the model is incomplete.");
            }

            logger.MailAssessed(elapsedMs);
            return content.GetString() ?? string.Empty;
        }
        catch (HttpRequestException exception)
        {
            // The message may name the host; the type of the cause is enough.
            logger.ServiceUnreachable(exception.InnerException?.GetType().Name ?? exception.GetType().Name);
            throw new LlmUnavailableException("LLM service not reachable", exception);
        }
        catch (OperationCanceledException exception) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            logger.TimedOut(settings.Timeout.TotalSeconds);
            throw new LlmUnavailableException("The model did not answer in time.", exception);
        }
        catch (JsonException exception)
        {
            logger.AnswerUnusable("invalid-response");
            throw new InvalidDataException("LLM service answer is no JSON", exception);
        }
    }

    private JsonObject Request(MailText mail, bool knownSender)
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
            ["format"] = MailSchema.Create(),
            ["options"] = options,
            ["messages"] = new JsonArray(
                new JsonObject { ["role"] = "system", ["content"] = MailPrompt.System },
                new JsonObject { ["role"] = "user", ["content"] = MailPrompt.User(mail, knownSender) }),
        };
    }
}
