using System.Globalization;
using Microsoft.Extensions.Configuration;
using NoMorePaper.Application.Processing;

namespace NoMorePaper.Infrastructure.Recognition;

/// <summary>
/// Where and how the Worker asks the local LLM (docs/kb/erkennung.md, "Vorgaben für den Worker"),
/// from configuration section <c>NoMorePaper:Recognition</c> (env <c>NoMorePaper__Recognition__…</c>).
/// </summary>
/// <param name="Endpoint">Base address of the Ollama service; only inside the own network (ADR-0002).</param>
/// <param name="Model">Ollama model name, e.g. <c>ministral-3:8b-instruct-2512-q4_K_M</c> (ADR-0022).</param>
/// <param name="Timeout">Time limit of one recognition request.</param>
/// <param name="NumThread">CPU threads for the model; 0 lets Ollama choose.</param>
public sealed record OllamaSettings(Uri Endpoint, string Model, TimeSpan Timeout, int NumThread)
{
    public const string EndpointKey = "NoMorePaper:Recognition:Endpoint";
    public const string ModelKey = "NoMorePaper:Recognition:Model";

    /// <summary>A <see cref="TimeSpan"/> (<c>00:05:00</c>) or whole seconds (<c>300</c>).</summary>
    public const string TimeoutKey = "NoMorePaper:Recognition:Timeout";

    public const string NumThreadKey = "NoMorePaper:Recognition:NumThread";

    /// <summary>The service in docker-compose.yml.</summary>
    public const string DefaultEndpoint = "http://ollama:11434";

    /// <summary>Chosen in the spike (ADR-0022); <c>qwen2.5:7b-instruct-q4_K_M</c> is the documented fallback.</summary>
    public const string DefaultModel = "ministral-3:8b-instruct-2512-q4_K_M";

    /// <summary>Covers two cores, a long document and loading the model (spike).</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(300);

    /// <summary>
    /// Longest time limit allowed: well within the lock of a job (<see cref="ProcessNextDocument.LockFor"/>),
    /// so that no second Worker takes the job over while the model is still answering.
    /// </summary>
    public static readonly TimeSpan MaxTimeout = ProcessNextDocument.LockFor - TimeSpan.FromMinutes(5);

    /// <summary>The cores the spike measured with; set it to the cores of the Ollama container.</summary>
    public const int DefaultNumThread = 4;

    /// <exception cref="InvalidOperationException">A value is invalid; the message names the key, never the value.</exception>
    public static OllamaSettings Read(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var endpoint = Text(configuration, EndpointKey) ?? DefaultEndpoint;
        if (!Uri.TryCreate(endpoint.EndsWith('/') ? endpoint : endpoint + "/", UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
        {
            throw Invalid(EndpointKey);
        }

        var timeout = DefaultTimeout;
        if (Text(configuration, TimeoutKey) is { } timeoutText)
        {
            timeout = int.TryParse(timeoutText, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
                ? TimeSpan.FromSeconds(seconds)
                : TimeSpan.TryParse(timeoutText, CultureInfo.InvariantCulture, out var parsed) ? parsed : TimeSpan.Zero;
            if (timeout <= TimeSpan.Zero || timeout > MaxTimeout)
            {
                throw Invalid(TimeoutKey);
            }
        }

        var threads = DefaultNumThread;
        if (Text(configuration, NumThreadKey) is { } threadText
            && (!int.TryParse(threadText, NumberStyles.None, CultureInfo.InvariantCulture, out threads)))
        {
            throw Invalid(NumThreadKey);
        }

        return new OllamaSettings(uri, Text(configuration, ModelKey) ?? DefaultModel, timeout, threads);
    }

    private static string? Text(IConfiguration configuration, string key) =>
        configuration[key] is { } value && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

    private static InvalidOperationException Invalid(string key) => new($"Configuration value '{key}' is invalid.");
}
