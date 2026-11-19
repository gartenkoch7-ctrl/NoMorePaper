using NoMorePaper.Application.Processing;
using NoMorePaper.Application.Recognition;
using NoMorePaper.Worker.Recognition;
using NoMorePaper.Worker.Tests.Persistence;
using NoMorePaper.Worker.Tests.Processing;
using Xunit;

namespace NoMorePaper.Worker.Tests.Recognition;

[Collection(SharedPostgres.Name)]
public sealed class RecognitionStartupTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Start_RetriesUntilTheModelIsReady_ThenQueuesTheBacklog()
    {
        await using var harness = await ProcessorHarness.CreateAsync(postgres, "recognition_startup");
        var old = await harness.AddDoneDocumentAsync(ProcessorHarness.Owner, isPrivate: false, "Alter Brief");
        harness.Recognizer.PrepareFailures = 2;
        var log = new RecordingLogger<RecognitionStartup>();
        var readiness = new RecognitionReadiness();
        using var startup = new RecognitionStartup(harness.Recognizer, readiness, harness.Scopes, new ImmediateTimers(), log);

        await startup.StartAsync(Ct);
        await startup.ExecuteTask!.WaitAsync(Ct);

        Assert.True(readiness.IsReady);
        Assert.Equal(3, harness.Recognizer.PrepareCalls);
        Assert.Equal([2200, 2201, 2201, 2202], log.EventIds);
        Assert.Equal((JobStage.Recognition, 10), ((await harness.JobAsync(old.Id))!.Stage, (await harness.JobAsync(old.Id))!.Priority));
    }

    // Waits end at once, so the test does not sit through the retry delays.
    private sealed class ImmediateTimers : TimeProvider
    {
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            base.CreateTimer(callback, state, TimeSpan.Zero, period);
    }
}
