using NoMorePaper.Application.Processing;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Domain.Processing;
using NoMorePaper.Infrastructure.Editing;
using NoMorePaper.Worker.Tests.Persistence;
using Xunit;

namespace NoMorePaper.Worker.Tests.Processing;

[Collection(SharedPostgres.Name)]
public sealed class ProcessNextDocumentTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset s_start = ProcessorHarness.Start;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ProcessNext_ReturnsFalse_WhenNoJobIsDue()
    {
        await using var harness = await ProcessorHarness.CreateAsync(postgres, "processor_nothing_due");

        Assert.False(await harness.Processor.ProcessNextAsync(Ct));
        Assert.Equal(0, harness.Ocr.Calls);
    }

    [Fact]
    public async Task ProcessNext_CompletesTheDocument_FromAWorkingCopy_AndCleansUp()
    {
        await using var harness = await ProcessorHarness.CreateAsync(postgres, "processor_success");
        var id = await harness.AddQueuedDocumentAsync();

        Assert.True(await harness.Processor.ProcessNextAsync(Ct));

        var document = await harness.DocumentAsync(id);
        Assert.Equal((ProcessingState.Done, FakeOcrEngine.Text), (document.State, document.ContentText));
        Assert.Equal("Scan 1.pdf", document.FindFile(DocumentFileKind.Archive)?.FileName);
        Assert.Equal(JobStage.Recognition, (await harness.JobAsync(id))?.Stage);

        // OCR saw a working copy with a fixed name in the working folder, never the stored original.
        var (input, existed) = harness.Ocr.LastInput!.Value;
        Assert.True(existed);
        Assert.Equal(Path.Combine(Path.GetDirectoryName(input)!, "original"), input);
        Assert.StartsWith(harness.WorkingFolder.Root, input, StringComparison.Ordinal);
        Assert.Empty(harness.WorkingFolderEntries());

        await using var archive = harness.Storage.OpenRead(id, DocumentFileKind.Archive);
        Assert.Equal("%PDF-1.7 archive", await new StreamReader(archive).ReadToEndAsync(Ct));
    }

    [Fact]
    public async Task ProcessNext_StoresThePagesOfTheArchive_WithIt()
    {
        await using var harness = await ProcessorHarness.CreateAsync(postgres, "processor_pages");
        var id = await harness.AddQueuedDocumentAsync();

        Assert.True(await harness.Processor.ProcessNextAsync(Ct));

        Assert.True(harness.Pages.ArchiveExisted);
        await using var db = harness.OtherWorkerContext();
        var pages = await new EfDocumentPages(db).ListAsync(id, Ct);
        Assert.Equal(harness.Pages.Pages, pages);
    }

    [Fact]
    public async Task ProcessNext_SchedulesARetry_AfterAFailedAttempt_AndSucceedsLater()
    {
        await using var harness = await ProcessorHarness.CreateAsync(postgres, "processor_retry");
        var id = await harness.AddQueuedDocumentAsync();
        harness.Ocr.Behavior = FakeOcrEngine.Throws(new ExternalToolException("ocrmypdf", 7));

        Assert.True(await harness.Processor.ProcessNextAsync(Ct));

        var job = await harness.JobAsync(id);
        Assert.Equal((1, s_start.AddMinutes(1), (DateTimeOffset?)null), (job!.Attempt, job.NextAttemptAt, job.LockedUntil));
        Assert.Equal("ExternalToolException: ocrmypdf exited with code 7", job.LastError);
        Assert.Equal(ProcessingState.Processing, (await harness.DocumentAsync(id)).State);
        Assert.Empty(harness.WorkingFolderEntries());

        // Not due before its time; then the second attempt succeeds.
        Assert.False(await harness.Processor.ProcessNextAsync(Ct));
        harness.Time.Now = s_start.AddMinutes(1);
        harness.Ocr.Behavior = FakeOcrEngine.WriteArchiveAsync;
        Assert.True(await harness.Processor.ProcessNextAsync(Ct));

        Assert.Equal(ProcessingState.Done, (await harness.DocumentAsync(id)).State);
        Assert.Equal(2, harness.Ocr.Calls);
    }

    [Fact]
    public async Task ProcessNext_GivesUp_WhenTheLastAttemptFails()
    {
        await using var harness = await ProcessorHarness.CreateAsync(postgres, "processor_give_up");
        var id = await harness.AddQueuedDocumentAsync(attemptsSoFar: RetryPolicy.MaxAttempts - 1);
        harness.Ocr.Behavior = FakeOcrEngine.Throws(new TimeoutException("ocrmypdf took /data/inbox/secret.pdf too long"));

        Assert.True(await harness.Processor.ProcessNextAsync(Ct));

        var document = await harness.DocumentAsync(id);
        Assert.Equal((ProcessingState.Failed, nameof(TimeoutException)), (document.State, document.FailureReason));
        Assert.Null(await harness.JobAsync(id));
        Assert.Contains(2104, harness.Log.EventIds);
        Assert.DoesNotContain(harness.Log.Entries, entry => entry.Message.Contains("secret", StringComparison.Ordinal));
        Assert.Empty(harness.WorkingFolderEntries());
    }

    [Fact]
    public async Task ProcessNext_GivesUp_WithoutOcr_WhenEveryAttemptWasInterrupted()
    {
        await using var harness = await ProcessorHarness.CreateAsync(postgres, "processor_interrupted_every_attempt");
        var id = await harness.AddQueuedDocumentAsync(attemptsSoFar: RetryPolicy.MaxAttempts);

        Assert.True(await harness.Processor.ProcessNextAsync(Ct));

        var document = await harness.DocumentAsync(id);
        Assert.Equal((ProcessingState.Failed, "Processing was interrupted in every attempt"), (document.State, document.FailureReason));
        Assert.Equal(0, harness.Ocr.Calls);
    }

    [Theory]
    [InlineData("encrypted")]
    [InlineData("unsupported")]
    public async Task ProcessNext_FailsAtOnce_WhenTheFailureIsPermanent(string failure)
    {
        await using var harness = await ProcessorHarness.CreateAsync(postgres, $"processor_permanent_{failure}");
        var id = await harness.AddQueuedDocumentAsync();
        Exception error = failure == "encrypted"
            ? new ExternalToolException("ocrmypdf", 8, isPermanent: true)
            : new NotSupportedException("The media type cannot be processed.");
        harness.Ocr.Behavior = FakeOcrEngine.Throws(error);

        Assert.True(await harness.Processor.ProcessNextAsync(Ct));

        var document = await harness.DocumentAsync(id);
        var reason = failure == "encrypted" ? "ExternalToolException: ocrmypdf exited with code 8" : nameof(NotSupportedException);
        Assert.Equal((ProcessingState.Failed, reason), (document.State, document.FailureReason));
        Assert.Null(await harness.JobAsync(id));
        Assert.Equal(1, harness.Ocr.Calls);
        Assert.Empty(harness.WorkingFolderEntries());
    }
}
