using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Application.Inputs.Mail;
using NoMorePaper.Infrastructure.Recognition;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Recognition;

/// <summary>The mail check over Ollama's HTTP API, with a fake service.</summary>
public sealed class OllamaMailAssessorTests : IDisposable
{
    private static readonly OllamaSettings s_settings = new(new Uri("http://ollama.test/"), OllamaSettings.DefaultModel, TimeSpan.FromSeconds(30), 4);

    private static readonly MailText s_mail = new("stadtwerke.example", "Abschlag", "Neuer Abschlag ab 01.12.2026: 86,00 EUR per Lastschrift.", ["application/pdf"]);

    private readonly FakeOllamaHandler _ollama = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _ollama.Dispose();

    [Fact]
    public async Task Assess_AsksLikeRecognition_WithTheMailSchema_AndReadsTheAnswer()
    {
        _ollama.Reply(HttpStatusCode.OK, FakeOllamaHandler.ChatAnswer("""{"isSpam":false,"isDocument":true,"note":"Neuer Abschlag ab 01.12.2026 per Lastschrift."}"""));

        var answer = await Assessor().AssessAsync(s_mail, knownSender: false, Ct);

        Assert.Equal(new MailAssessment(false, true, "Neuer Abschlag ab 01.12.2026 per Lastschrift."), answer);
        var (path, body) = Assert.Single(_ollama.Requests);
        Assert.Equal("/api/chat", path);
        Assert.Equal((OllamaSettings.DefaultModel, OllamaRecognizer.ContextTokens, OllamaRecognizer.KeepAlive), ((string?)body["model"], (int?)body["options"]!["num_ctx"], (string?)body["keep_alive"]));
        Assert.Equal(["isSpam", "isDocument", "note"], body["format"]!["required"]!.AsArray().Select(node => (string?)node));
        var user = (string?)body["messages"]![1]!["content"];
        Assert.Contains("Domain des Absenders: stadtwerke.example", user, StringComparison.Ordinal);
        Assert.Contains("Anhänge: 1 PDF", user, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", (string?)body["messages"]![0]!["content"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task AKnownSender_IsToldSo_AndIsNeverSpam()
    {
        _ollama.Reply(HttpStatusCode.OK, FakeOllamaHandler.ChatAnswer("""{"isSpam":true,"isDocument":true,"note":""}"""));

        var answer = await Assessor().AssessAsync(s_mail, knownSender: true, Ct);

        Assert.False(answer.IsSpam);
        Assert.Contains("dem Haushalt bekannt", (string?)_ollama.Requests[0].Body["messages"]![1]!["content"], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task ARefusingService_IsUnavailable_NotAFaultOfTheMail(HttpStatusCode status)
    {
        _ollama.Reply(status);

        await Assert.ThrowsAsync<LlmUnavailableException>(() => Assessor().AssessAsync(s_mail, false, Ct));
    }

    [Fact]
    public async Task AnUnreachableOrSlowService_IsUnavailable()
    {
        _ollama.Reply(_ => throw new HttpRequestException("refused", new System.Net.Sockets.SocketException()));
        await Assert.ThrowsAsync<LlmUnavailableException>(() => Assessor().AssessAsync(s_mail, false, Ct));

        var slow = new FakeOllamaHandler().Reply(async token =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var settings = s_settings with { Timeout = TimeSpan.FromMilliseconds(50) };
        await Assert.ThrowsAsync<LlmUnavailableException>(() => Assessor(slow, settings).AssessAsync(s_mail, false, Ct));
    }

    [Fact]
    public async Task ACutAnswer_IsUnusable()
    {
        _ollama.Reply(HttpStatusCode.OK, FakeOllamaHandler.ChatAnswer("""{"isSpam":fal""", "length"));

        await Assert.ThrowsAsync<InvalidDataException>(() => Assessor().AssessAsync(s_mail, false, Ct));
    }

    [Fact]
    public async Task ABusyLlm_IsUnavailable_WithoutAsking()
    {
        // The gate measures the wait; its 4 minutes pass at once here.
        var gate = new LlmGate(new ShortWait(), NullLogger<LlmGate>.Instance);
        using var recognition = await gate.EnterAsync(LlmPriority.Recognition, TimeSpan.FromSeconds(1), Ct);
        var assessor = new OllamaMailAssessor(
            new HttpClient(_ollama) { BaseAddress = s_settings.Endpoint }, s_settings, gate, TimeProvider.System, NullLogger<OllamaMailAssessor>.Instance);

        await Assert.ThrowsAsync<LlmUnavailableException>(() => assessor.AssessAsync(s_mail, false, Ct));
        Assert.Empty(_ollama.Requests);
    }

    private OllamaMailAssessor Assessor(FakeOllamaHandler? handler = null, OllamaSettings? settings = null) =>
        new(new HttpClient(handler ?? _ollama) { BaseAddress = s_settings.Endpoint }, settings ?? s_settings, new LlmGate(TimeProvider.System, NullLogger<LlmGate>.Instance), TimeProvider.System, NullLogger<OllamaMailAssessor>.Instance);

    // The gate's wait of 4 minutes passes at once: timers of this provider fire after 10 ms.
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
