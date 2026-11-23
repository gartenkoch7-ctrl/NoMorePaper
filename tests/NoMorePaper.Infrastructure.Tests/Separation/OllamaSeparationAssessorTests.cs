using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Application.Editing;
using NoMorePaper.Application.Recognition;
using NoMorePaper.Application.Separation;
using NoMorePaper.Infrastructure.Recognition;
using NoMorePaper.Infrastructure.Tests.Recognition;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Separation;

/// <summary>The stack separation over Ollama's HTTP API, with a fake service.</summary>
public sealed class OllamaSeparationAssessorTests : IDisposable
{
    private static readonly OllamaSettings s_settings = new(new Uri("http://ollama.test/"), OllamaSettings.DefaultModel, TimeSpan.FromSeconds(30), 4);

    private static readonly PageInfo[] s_stack =
    [
        new(1, "Stadtwerke München\r\nSehr geehrter Herr Mustermann,", false, false),
        new(2, string.Empty, true, false),
        new(3, "AOK Bayern\nIhre neue Gesundheitskarte", false, false),
    ];

    private readonly FakeOllamaHandler _ollama = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _ollama.Dispose();

    [Fact]
    public async Task Assess_AsksLikeRecognition_WithThePageStarts_AndReadsTheAnswer()
    {
        _ollama.Reply(HttpStatusCode.OK, FakeOllamaHandler.ChatAnswer("""{"senders":["Stadtwerke München","","AOK Bayern"],"boundaries":[3]}"""));

        var answer = await Assessor().AssessAsync(s_stack, Ct);

        Assert.Equal([3], answer.Boundaries);
        Assert.Equal(["Stadtwerke München", "", "AOK Bayern"], answer.Senders);
        var (path, body) = Assert.Single(_ollama.Requests);
        Assert.Equal("/api/chat", path);
        Assert.Equal((OllamaSettings.DefaultModel, OllamaRecognizer.ContextTokens, OllamaRecognizer.KeepAlive), ((string?)body["model"], (int?)body["options"]!["num_ctx"], (string?)body["keep_alive"]));
        Assert.Equal(["senders", "boundaries"], body["format"]!["required"]!.AsArray().Select(node => (string?)node));
        var user = (string?)body["messages"]![1]!["content"];
        Assert.Contains("Der Stapel hat 3 Seiten.", user, StringComparison.Ordinal);
        Assert.Contains("Seite 2 (vermutlich leer):", user, StringComparison.Ordinal);
        Assert.Contains("Seite 3:\n<<<\nAOK Bayern\nIhre neue Gesundheitskarte\n>>>", user, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", user, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", (string?)body["messages"]![0]!["content"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Assess_AsksAboutThirtyPagesAtMost()
    {
        var pages = Enumerable.Range(1, 40).Select(page => new PageInfo(page, "Seite " + page, false, false)).ToList();
        _ollama.Reply(HttpStatusCode.OK, FakeOllamaHandler.ChatAnswer("""{"senders":[],"boundaries":[30,31]}"""));

        var answer = await Assessor().AssessAsync(pages, Ct);

        Assert.Equal([30], answer.Boundaries);
        var user = (string?)_ollama.Requests[0].Body["messages"]![1]!["content"];
        Assert.Contains("Seite 30:", user, StringComparison.Ordinal);
        Assert.DoesNotContain("Seite 31:", user, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"senders":[],"boundaries":[1,2,2,9,"3",2.5,3]}""", new[] { 2, 3 }, 5)]
    [InlineData("""{"senders":[],"boundaries":[]}""", new int[0], 0)]
    public void Parse_KeepsPlausibleBoundaries_Only(string content, int[] boundaries, int dropped)
    {
        var answer = SeparationAnswer.Parse(content, 3, out var droppedCount);

        Assert.Equal(boundaries, answer.Boundaries);
        Assert.Equal(dropped, droppedCount);
    }

    [Fact]
    public void Parse_CutsLongSenders_AndKeepsOnePerPage()
    {
        var answer = SeparationAnswer.Parse($$"""{"senders":[" {{new string('A', 200)}} ",4,"B","C"],"boundaries":[]}""", 2, out _);

        Assert.Equal([new string('A', SeparationAnswer.MaxSenderLength), string.Empty], answer.Senders);
    }

    [Theory]
    [InlineData("""{"senders":[]}""")]
    [InlineData("""{"boundaries":[2],"senders":"x"}""")]
    [InlineData("""[2]""")]
    [InlineData("""{"boundaries":[2""")]
    public void Parse_RefusesAnAnswerWithoutBothArrays(string content) =>
        Assert.Throws<InvalidDataException>(() => SeparationAnswer.Parse(content, 3, out _));

    [Fact]
    public async Task ARefusingUnreachableOrCutService_FailsTheRequest()
    {
        _ollama.Reply(HttpStatusCode.InternalServerError);
        await Assert.ThrowsAsync<RecognitionException>(() => Assessor().AssessAsync(s_stack, Ct));

        _ollama.Reply(_ => throw new HttpRequestException("refused", new System.Net.Sockets.SocketException()));
        await Assert.ThrowsAsync<RecognitionException>(() => Assessor().AssessAsync(s_stack, Ct));

        _ollama.Reply(HttpStatusCode.OK, FakeOllamaHandler.ChatAnswer("""{"senders":["Stadt""", "length"));
        await Assert.ThrowsAsync<InvalidDataException>(() => Assessor().AssessAsync(s_stack, Ct));
    }

    [Fact]
    public async Task ASlowService_TimesOut()
    {
        _ollama.Reply(async token =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        await Assert.ThrowsAsync<TimeoutException>(() => Assessor(s_settings with { Timeout = TimeSpan.FromMilliseconds(50) }).AssessAsync(s_stack, Ct));
    }

    [Fact]
    public async Task ABusyLlm_IsReported_WithoutAsking()
    {
        var gate = new LlmGate(new ShortWait(), NullLogger<LlmGate>.Instance);
        using var recognition = await gate.EnterAsync(LlmPriority.Recognition, TimeSpan.FromSeconds(1), Ct);
        using var assessor = new OllamaSeparationAssessor(
            new HttpClient(_ollama) { BaseAddress = s_settings.Endpoint }, s_settings, gate, TimeProvider.System, NullLogger<OllamaSeparationAssessor>.Instance);

        await Assert.ThrowsAsync<LlmBusyException>(() => assessor.AssessAsync(s_stack, Ct));
        Assert.Empty(_ollama.Requests);
    }

    private OllamaSeparationAssessor Assessor(OllamaSettings? settings = null) =>
        new(new HttpClient(_ollama, disposeHandler: false) { BaseAddress = s_settings.Endpoint }, settings ?? s_settings, new LlmGate(TimeProvider.System, NullLogger<LlmGate>.Instance), TimeProvider.System, NullLogger<OllamaSeparationAssessor>.Instance);

    // The gate's wait passes at once: timers of this provider fire after 10 ms.
    private sealed class ShortWait : TimeProvider
    {
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            new ShortTimer(System.CreateTimer(callback, state, Shorten(dueTime), period));

        private static TimeSpan Shorten(TimeSpan due) => due == Timeout.InfiniteTimeSpan ? due : TimeSpan.FromMilliseconds(10);

        // A CancellationTokenSource creates its timer stopped and starts it with Change.
        private sealed class ShortTimer(ITimer inner) : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => inner.Change(Shorten(dueTime), period);

            public void Dispose() => inner.Dispose();

            public ValueTask DisposeAsync() => inner.DisposeAsync();
        }
    }
}
