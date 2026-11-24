using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Processing;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Worker.Tests.Persistence;
using Xunit;

namespace NoMorePaper.Worker.Tests.Processing;

[Collection(SharedPostgres.Name)]
public sealed class ProcessNextDocumentStageTests(PostgresFixture postgres)
{
    private const JobStage UnknownStage = (JobStage)99;

    private static readonly DateTimeOffset s_start = ProcessorHarness.Start;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ProcessNext_FailsTheAttempt_WhenNoRunnerHandlesTheStage_AndTheQueueMovesOn()
    {
        await using var harness = await ProcessorHarness.CreateAsync(postgres, "processor_stage_without_runner");
        var waiting = await harness.AddQueuedDocumentAsync();
        await using (var db = harness.OtherWorkerContext())
        {
            // A stage no runner handles (one a newer build queued); the job is due before the next one.
            await db.ProcessingJobs.Where(job => job.DocumentId == waiting)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(job => job.Stage, UnknownStage).SetProperty(job => job.NextAttemptAt, s_start.AddSeconds(-1)),
                    Ct);
        }

        var next = await harness.AddQueuedDocumentAsync();

        Assert.True(await harness.Processor.ProcessNextAsync(Ct));

        var job = await harness.JobAsync(waiting);
        Assert.Equal((UnknownStage, 1, (Guid?)null), (job!.Stage, job.Attempt, job.ClaimId));
        Assert.Equal(s_start.AddMinutes(1), job.NextAttemptAt);
        Assert.Equal("No runner for stage 99", job.LastError);
        Assert.Contains(5000, harness.Log.EventIds);
        Assert.Equal(0, harness.Ocr.Calls);

        // The job behind it is not blocked, and the failed one waits for its retry delay.
        Assert.True(await harness.Processor.ProcessNextAsync(Ct));
        Assert.Equal(1, harness.Ocr.Calls);
        Assert.Equal(ProcessingState.Done, (await harness.DocumentAsync(next)).State);
        Assert.Equal(1, (await harness.JobAsync(waiting))!.Attempt);
    }
}
