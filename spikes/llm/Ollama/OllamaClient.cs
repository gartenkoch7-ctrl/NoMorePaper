using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LlmSpike.Ollama;

/// <summary>Minimal client for the local Ollama HTTP API. Talks to localhost only (AGENTS.md 2.4).</summary>
public sealed class OllamaClient : IDisposable
{
    private readonly HttpClient http;

    public OllamaClient(Uri baseAddress)
    {
        // A long document on four CPU cores can take many minutes; the spike wants the number, not a timeout.
        this.http = new HttpClient { BaseAddress = baseAddress, Timeout = TimeSpan.FromMinutes(30) };
    }

    public async Task<string> VersionAsync(CancellationToken cancellationToken)
    {
        var node = await this.http.GetFromJsonAsync<JsonNode>("api/version", cancellationToken).ConfigureAwait(false);
        return node?["version"]?.GetValue<string>() ?? "unknown";
    }

    /// <summary>Memory the loaded model occupies according to Ollama (weights plus context), in bytes.</summary>
    public async Task<long> LoadedBytesAsync(string model, CancellationToken cancellationToken)
    {
        var node = await this.http.GetFromJsonAsync<JsonNode>("api/ps", cancellationToken).ConfigureAwait(false);
        foreach (var entry in node?["models"]?.AsArray() ?? [])
        {
            if (entry?["name"]?.GetValue<string>() == model)
            {
                return entry["size"]?.GetValue<long>() ?? 0;
            }
        }

        return 0;
    }

    public async Task<ChatResult> ChatAsync(
        string model,
        string system,
        string user,
        JsonObject schema,
        int contextTokens,
        int threads,
        CancellationToken cancellationToken)
    {
        var options = new JsonObject
        {
            ["temperature"] = 0,
            ["seed"] = 1,
            ["num_ctx"] = contextTokens,
            // The answer is about 150 tokens; the cap stops a runaway generation.
            ["num_predict"] = 400,
        };
        if (threads > 0)
        {
            options["num_thread"] = threads;
        }

        var request = new JsonObject
        {
            ["model"] = model,
            ["stream"] = false,
            ["keep_alive"] = "30m",
            ["format"] = schema,
            ["options"] = options,
            ["messages"] = new JsonArray(
                new JsonObject { ["role"] = "system", ["content"] = system },
                new JsonObject { ["role"] = "user", ["content"] = user }),
        };

        var watch = Stopwatch.StartNew();
        using var response = await this.http.PostAsJsonAsync("api/chat", request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        watch.Stop();
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Ollama answered {(int)response.StatusCode}: {body}");
        }

        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        return new ChatResult(
            root.GetProperty("message").GetProperty("content").GetString() ?? string.Empty,
            watch.Elapsed.TotalSeconds,
            Seconds(root, "load_duration"),
            Seconds(root, "prompt_eval_duration"),
            Count(root, "prompt_eval_count"),
            Seconds(root, "eval_duration"),
            Count(root, "eval_count"),
            root.TryGetProperty("done_reason", out var reason) ? reason.GetString() : null);
    }

    public void Dispose() => this.http.Dispose();

    private static double Seconds(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) ? value.GetInt64() / 1e9 : 0;

    private static int Count(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) ? value.GetInt32() : 0;
}
