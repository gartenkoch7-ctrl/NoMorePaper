using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace NoMorePaper.Infrastructure.Tests.Recognition;

/// <summary>Stands in for the Ollama service: answers each request with the next queued reply and keeps what was sent.</summary>
internal sealed class FakeOllamaHandler : HttpMessageHandler
{
    private readonly Queue<Func<CancellationToken, Task<HttpResponseMessage>>> _replies = new();

    public List<(string Path, JsonNode Body)> Requests { get; } = [];

    public static string ChatAnswer(string content, string doneReason = "stop") => new JsonObject
    {
        ["model"] = "ministral-3:8b-instruct-2512-q4_K_M",
        ["message"] = new JsonObject { ["role"] = "assistant", ["content"] = content },
        ["done"] = true,
        ["done_reason"] = doneReason,
        ["load_duration"] = 7_000_000_000L,
        ["prompt_eval_count"] = 912,
        ["eval_count"] = 151,
    }.ToJsonString();

    public FakeOllamaHandler Reply(HttpStatusCode status, string body = "{}")
    {
        _replies.Enqueue(_ => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        }));
        return this;
    }

    public FakeOllamaHandler Reply(Func<CancellationToken, Task<HttpResponseMessage>> reply)
    {
        _replies.Enqueue(reply);
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? "null" : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request.RequestUri!.AbsolutePath, JsonNode.Parse(body)!));
        return await _replies.Dequeue()(cancellationToken);
    }
}
