using NoMorePaper.Application.Processing;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Processing;
using NoMorePaper.Worker.Processing;
using NoMorePaper.Worker.Tests.Persistence;
using Xunit;

namespace NoMorePaper.Worker.Tests.Processing;

/// <summary>Attempts that do not end on their own: the Worker stops, or another Worker takes the job over.</summary>
[Collection(SharedPostgres.Name)]
public sealed class DocumentProcessorInterruptionTests(PostgresFixture postgres)
{
    private const int DocumentInterrupted = 2108;
    private const int ClaimLost = 2110;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Shutdown_HandsTheJobBack_WithoutCountingTheAttempt_AndCleansUp()
    {
        await using var harness = await ProcessorHarness.CreateAsync(postgres, "processor_shutdown");
        var id = await harness.AddQueuedDocumentAsync();
        harness.Ocr.Behavior = FakeOcrEngine.WaitForCancellationAsync;
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(Ct);

        var run = harness.Processor.ProcessNextAsync(stopping.Token);
        await harness.Ocr.Started.Task.WaitAsync(Ct);
        await stopping.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        var job = await harness.JobAsync(id);
        Assert.Equal((0, (DateTimeOffset?)null, ProcessorHarness.Start), (job!.Attempt, job.LockedUntil, job.NextAttemptAt));
        Assert.Equal(((Guid?)null, (string?)null), (job.ClaimId, job.LastError));
        Assert.Contains(DocumentInterrupted, harness.Log.EventIds);
        Assert.Empty(harness.WorkingFolderEntries());

        // The next start takes it up at once, as its first attempt.
        harness.Ocr.Behavior = FakeOcrEngine.WriteArchiveAsync;
        Assert.True(await harness.Processor.ProcessNextAsync(Ct));
        Assert.Equal(ProcessingState.Done, (await harness.DocumentAsync(id)).State);
    }

    [Fact]
    public async Task Shutdown_HandsTheJobBack_WhenTheStopMakesTheToolFail()
    {
        await using var harness = await ProcessorHarness.CreateAsync(postgres, "processor_shutdown_tool_fails");
        var id = await harness.AddQueuedDocumentAsync();

        // A killed tool reports an exit code, not a cancellation.
        harness.Ocr.Behavior = async (input, output, cancellationToken) =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            throw new ExternalToolException("ocrmypdf", 137, isPermanent: true);
        };
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(Ct);

        var run = harness.Processor.ProcessNextAsync(stopping.Token);
        await harness.Ocr.Started.Task.WaitAsync(Ct);
        await stopping.CancelAsync();

        var stopped = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        Assert.IsType<ExternalToolException>(stopped.InnerException);
        var job = await harness.JobAsync(id);
        Assert.Equal((0, (DateTimeOffset?)null, (Guid?)null, (string?)null), (job!.Attempt, job.LockedUntil, job.ClaimId, job.LastError));
        Assert.Equal(ProcessingState.Processing, (await harness.DocumentAsync(id)).State);
        Assert.Contains(DocumentInterrupted, harness.Log.EventIds);
        Assert.Empty(harness.WorkingFolderEntries());
    }

    [Fact]
    public async Task LostClaim_DiscardsTheResult_AndLeavesTheJobToTheNewOwner()
    {
        await using var harness = await ProcessorHarness.CreateAsync(postgres, "processor_claim_lost_complete");
        var id = await harness.AddQueuedDocumentAsync();
        harness.Ocr.Behavior = async (input, output, cancellationToken) =>
        {
            await TakeOverAsync(harness);
            return await FakeOcrEngine.WriteArchiveAsync(input, output, cancellationToken);
        };

        Assert.True(await harness.Processor.ProcessNextAsync(Ct));

        var document = await harness.DocumentAsync(id);
        Assert.Equal(ProcessingState.Processing, document.State);
        Assert.Null(document.ContentText);
        Assert.Equal(2, (await harness.JobAsync(id))?.Attempt);
        Assert.Contains(ClaimLost, harness.Log.EventIds);
        Assert.Empty(harness.WorkingFolderEntries());
    }

    [Fact]
    public async Task LostClaim_DoesNotRecordTheFailure()
    {
        await using var harness = await ProcessorHarness.CreateAsync(postgres, "processor_claim_lost_fail");
        var id = await harness.AddQueuedDocumentAsync();
        harness.Ocr.Behavior = async (_, _, _) =>
        {
            await TakeOverAsync(harness);
            throw new ExternalToolException("ocrmypdf", 8, isPermanent: true);
        };

        Assert.True(await harness.Processor.ProcessNextAsync(Ct));

        var job = await harness.JobAsync(id);
        Assert.Equal((2, (string?)null), (job?.Attempt, job?.LastError));
        Assert.Equal(ProcessingState.Processing, (await harness.DocumentAsync(id)).State);
        Assert.Contains(ClaimLost, harness.Log.EventIds);
    }

    // The attempt outlived its lock, and another Worker claimed the job as attempt 2.
    private static async Task TakeOverAsync(ProcessorHarness harness)
    {
        harness.Time.Now = ProcessorHarness.Start + DocumentProcessor.LockFor + TimeSpan.FromSeconds(1);
        await using var db = harness.OtherWorkerContext();
        var claimed = await new JobQueue(db, harness.Time).ClaimNextAsync(DocumentProcessor.LockFor, Ct);
        Assert.Equal(2, claimed?.Attempt);
    }
}
