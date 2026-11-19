using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Application.Recognition;
using NoMorePaper.Infrastructure.Recognition;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Recognition;

public sealed class OllamaRecognizerTests : IDisposable
{
    private const string Answer = """
        {"correspondent":"Stadtwerke München","documentType":"Rechnung","summary":"Abschlag für November.","title":"Strom November 2026",
         "documentDate":"2026-11-01","amount":84.2,"currency":"EUR","tags":["Strom"]}
        """;

    private static readonly RecognitionRequest s_request = new(
        "Stadtwerke München\nRechnung 84,20 EUR", ["Stadtwerke München", "AOK"], ["Rechnung"], ["Strom"], []);

    private readonly FakeOllamaHandler _ollama = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Recognize_SendsTheRequestOfTheSpike_AndReadsTheAnswer()
    {
        _ollama.Reply(HttpStatusCode.OK, FakeOllamaHandler.ChatAnswer(Answer));
        using var recognizer = Recognizer();

        var result = await recognizer.RecognizeAsync(s_request, Ct);

        Assert.Equal(("Stadtwerke München", "Rechnung", "Strom November 2026"), (result.Correspondent, result.DocumentType, result.Title));
        Assert.Equal((new DateOnly(2026, 11, 1), 84.2m, "EUR"), (result.DocumentDate, result.Amount?.Value, result.Amount?.Currency));
        var (path, body) = Assert.Single(_ollama.Requests);
        Assert.Equal("/api/chat", path);
        Assert.Equal(("ministral-3:8b-instruct-2512-q4_K_M", false, "30m"), (Text(body, "model"), (bool)body["stream"]!, Text(body, "keep_alive")));
        var options = body["options"]!.AsObject();
        Assert.Equal(
            [("temperature", 0), ("seed", 1), ("num_ctx", 4096), ("num_predict", 400), ("num_thread", 4)],
            options.Select(option => (option.Key, (int)option.Value!)).ToArray());
        Assert.Equal(
            ["correspondent", "documentType", "summary", "title", "documentDate", "amount", "currency", "tags"],
            body["format"]!["properties"]!.AsObject().Select(property => property.Key).ToArray());
        var messages = body["messages"]!.AsArray();
        Assert.Contains("Absender: Stadtwerke München; AOK\n", Text(messages[0]!, "content"), StringComparison.Ordinal);
        Assert.Contains("<<<\nStadtwerke München\nRechnung 84,20 EUR\n>>>", Text(messages[1]!, "content"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Recognize_LeavesTheThreadsToOllama_WhenNoneAreConfigured()
    {
        _ollama.Reply(HttpStatusCode.OK, FakeOllamaHandler.ChatAnswer(Answer));
        using var recognizer = Recognizer(threads: 0);

        await recognizer.RecognizeAsync(s_request, Ct);

        Assert.False(_ollama.Requests[0].Body["options"]!.AsObject().ContainsKey("num_thread"));
    }

    [Theory]
    [InlineData("length", "answer ended with done_reason length")]
    [InlineData("load", "answer ended with done_reason other")]
    public async Task Recognize_Fails_WhenTheAnswerDidNotEndNormally(string doneReason, string message)
    {
        _ollama.Reply(HttpStatusCode.OK, FakeOllamaHandler.ChatAnswer("{\"correspondent\":\"Stadt", doneReason));
        using var recognizer = Recognizer();

        var error = await Assert.ThrowsAsync<RecognitionException>(() => recognizer.RecognizeAsync(s_request, Ct));

        Assert.Equal(message, error.Message);
    }

    [Theory]
    [InlineData("""{"message":{"content":"Rechnung"},"done_reason":"stop"}""", "answer is no JSON")]
    [InlineData("""{"done_reason":"stop"}""", "answer has no content")]
    [InlineData("<html>", "LLM service answer is no JSON")]
    public async Task Recognize_Fails_WhenTheAnswerCannotBeRead(string response, string message)
    {
        _ollama.Reply(HttpStatusCode.OK, response);
        using var recognizer = Recognizer();

        var error = await Assert.ThrowsAsync<RecognitionException>(() => recognizer.RecognizeAsync(s_request, Ct));

        Assert.Equal(message, error.Message);
    }

    [Fact]
    public async Task Recognize_Fails_WithTheStatusCodeOnly_WhenTheServiceRefuses()
    {
        _ollama.Reply(HttpStatusCode.InternalServerError, """{"error":"model runner has unexpectedly stopped"}""");
        using var recognizer = Recognizer();

        var error = await Assert.ThrowsAsync<RecognitionException>(() => recognizer.RecognizeAsync(s_request, Ct));

        Assert.Equal("LLM service answered 500", error.Message);
    }

    [Fact]
    public async Task Recognize_Fails_WithoutTheHost_WhenTheServiceIsUnreachable()
    {
        _ollama.Reply(_ => throw new HttpRequestException("Connection refused (ollama:11434)"));
        using var recognizer = Recognizer();

        var error = await Assert.ThrowsAsync<RecognitionException>(() => recognizer.RecognizeAsync(s_request, Ct));

        Assert.Equal("LLM service not reachable", error.Message);
    }

    [Fact]
    public async Task Recognize_TimesOut_AfterItsLimit_ButPassesTheCallersCancellationOn()
    {
        _ollama.Reply(WaitForeverAsync).Reply(WaitForeverAsync);
        using var recognizer = Recognizer(timeout: TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAsync<TimeoutException>(() => recognizer.RecognizeAsync(s_request, Ct));

        using var stopping = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        using var patient = Recognizer(timeout: TimeSpan.FromMinutes(5));
        _ollama.Requests.Clear();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => patient.RecognizeAsync(s_request, stopping.Token));
    }

    [Fact]
    public async Task Prepare_OnlyAsks_WhenTheModelIsThere()
    {
        _ollama.Reply(HttpStatusCode.OK);
        using var recognizer = Recognizer();

        await recognizer.PrepareAsync(Ct);

        var (path, body) = Assert.Single(_ollama.Requests);
        Assert.Equal(("/api/show", "ministral-3:8b-instruct-2512-q4_K_M"), (path, Text(body, "model")));
    }

    [Fact]
    public async Task Prepare_DownloadsAMissingModel()
    {
        _ollama.Reply(HttpStatusCode.NotFound).Reply(HttpStatusCode.OK, """{"status":"success"}""");
        using var recognizer = Recognizer();

        await recognizer.PrepareAsync(Ct);

        Assert.Equal("/api/pull", _ollama.Requests[1].Path);
        Assert.False((bool)_ollama.Requests[1].Body["stream"]!);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, HttpStatusCode.OK, "LLM service answered 500")]
    [InlineData(HttpStatusCode.NotFound, HttpStatusCode.BadGateway, "LLM service answered 502")]
    public async Task Prepare_Fails_WhenTheServiceRefuses(HttpStatusCode show, HttpStatusCode pull, string message)
    {
        _ollama.Reply(show).Reply(pull);
        using var recognizer = Recognizer();

        var error = await Assert.ThrowsAsync<RecognitionException>(() => recognizer.PrepareAsync(Ct));

        Assert.Equal(message, error.Message);
    }

    public void Dispose() => _ollama.Dispose();

    private OllamaRecognizer Recognizer(int threads = 4, TimeSpan? timeout = null)
    {
        var settings = new OllamaSettings(
            new Uri("http://ollama:11434/"), OllamaSettings.DefaultModel, timeout ?? OllamaSettings.DefaultTimeout, threads);
        var http = new HttpClient(_ollama, disposeHandler: false) { BaseAddress = settings.Endpoint, Timeout = Timeout.InfiniteTimeSpan };
        return new OllamaRecognizer(http, settings, TimeProvider.System, NullLogger<OllamaRecognizer>.Instance);
    }

    private static async Task<HttpResponseMessage> WaitForeverAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
        throw new InvalidOperationException("Not reached.");
    }

    private static string? Text(JsonNode node, string name) => (string?)node[name];
}
