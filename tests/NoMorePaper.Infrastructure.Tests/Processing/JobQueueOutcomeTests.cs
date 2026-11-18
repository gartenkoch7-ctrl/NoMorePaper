using NoMorePaper.Application.Documents;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Domain.Processing;
using NoMorePaper.Infrastructure.Processing;
using NoMorePaper.Infrastructure.Tests.Persistence;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Processing;

[Collection(SharedPostgres.Name)]
public sealed class JobQueueOutcomeTests(PostgresFixture postgres)
{
    private const string ToolError = "ExternalToolException: ocrmypdf exited with code 7";
    private static readonly DateTimeOffset s_start = QueueDatabase.Start;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Start_MarksTheDocumentAsProcessing()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "queue_start");
        var id = await data.AddQueuedDocumentAsync(s_start);
        await using var db = data.Context();
        var queue = data.Queue(db);
        var job = await queue.ClaimNextAsync(QueueDatabase.LockFor, Ct);

        var document = await queue.StartAsync(id, job!.Attempt, Ct);

        Assert.NotNull(document?.FindFile(DocumentFileKind.Original));
        Assert.Equal(ProcessingState.Processing, (await data.DocumentAsync(id)).State);
    }

    [Fact]
    public async Task Complete_StoresTextAndArchiveFile_AndRemovesTheJob()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "queue_complete");
        var id = await data.AddQueuedDocumentAsync(s_start, fileName: "Brief vom Amt.jpeg");
        await using (var db = data.Context())
        {
            var queue = data.Queue(db);
            var job = await queue.ClaimNextAsync(QueueDatabase.LockFor, Ct);
            await queue.StartAsync(id, job!.Attempt, Ct);
            Assert.True(await queue.CompleteAsync(id, job.Attempt, "Rechnung", new StoredFile(4711, QueueDatabase.Sha256), Ct));
        }

        var document = await data.DocumentAsync(id);
        Assert.Equal(ProcessingState.Done, document.State);
        Assert.Equal("Rechnung", document.ContentText);
        var archive = document.FindFile(DocumentFileKind.Archive);
        Assert.NotNull(archive);
        Assert.Equal(("Brief vom Amt.pdf", "application/pdf", 4711L), (archive.FileName, archive.MediaType, archive.SizeBytes));
        Assert.Null(await data.JobAsync(id));
    }

    [Fact]
    public async Task Fail_SchedulesTheNextAttempt_ReleasesTheLock_AndKeepsTheReasonShort()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "queue_fail_retry");
        var id = await data.AddQueuedDocumentAsync(s_start);
        RecordedFailure recorded;
        await using (var db = data.Context())
        {
            var queue = data.Queue(db);
            var job = await queue.ClaimNextAsync(QueueDatabase.LockFor, Ct);
            await queue.StartAsync(id, job!.Attempt, Ct);
            recorded = await queue.FailAsync(id, job.Attempt, new string('x', 5000), permanent: false, s_start.AddMinutes(2), Ct);
        }

        Assert.Equal(new RecordedFailure(FailureOutcome.RetryScheduled, s_start.AddMinutes(3)), recorded);
        var stored = await data.JobAsync(id);
        Assert.NotNull(stored);
        Assert.Equal((s_start.AddMinutes(3), (DateTimeOffset?)null), (stored.NextAttemptAt, stored.LockedUntil));
        Assert.Equal(Document.MaxFailureReasonLength, stored.LastError?.Length);
        Assert.Equal(ProcessingState.Processing, (await data.DocumentAsync(id)).State);
    }

    [Fact]
    public async Task Fail_GivesUpAfterTheFourthAttempt_AndMarksTheDocumentAsFailed()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "queue_fail_give_up");
        var id = await data.AddQueuedDocumentAsync(s_start);
        var retries = new List<DateTimeOffset?>();
        for (var attempt = 1; attempt <= RetryPolicy.MaxAttempts; attempt++)
        {
            await using var db = data.Context();
            var queue = data.Queue(db);
            var job = await queue.ClaimNextAsync(QueueDatabase.LockFor, Ct);
            Assert.Equal(attempt, job?.Attempt);
            await queue.StartAsync(id, attempt, Ct);
            var recorded = await queue.FailAsync(id, attempt, ToolError, permanent: false, data.Time.Now, Ct);
            retries.Add(recorded.NextAttemptAt);
            data.Time.Now = recorded.NextAttemptAt ?? data.Time.Now;
        }

        Assert.Equal(
            [s_start.AddMinutes(1), s_start.AddMinutes(6), s_start.AddMinutes(36), null],
            retries);
        var document = await data.DocumentAsync(id);
        Assert.Equal(ProcessingState.Failed, document.State);
        Assert.Equal(ToolError, document.FailureReason);
        Assert.Null(await data.JobAsync(id));
    }

    [Fact]
    public async Task Fail_GivesUpAtOnce_WhenTheFailureIsPermanent()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "queue_fail_permanent");
        var id = await data.AddQueuedDocumentAsync(s_start);
        RecordedFailure recorded;
        await using (var db = data.Context())
        {
            var queue = data.Queue(db);
            var job = await queue.ClaimNextAsync(QueueDatabase.LockFor, Ct);
            await queue.StartAsync(id, job!.Attempt, Ct);
            recorded = await queue.FailAsync(id, job.Attempt, "ExternalToolException: ocrmypdf exited with code 8", permanent: true, s_start, Ct);
        }

        Assert.Equal(new RecordedFailure(FailureOutcome.GivenUp), recorded);
        var document = await data.DocumentAsync(id);
        Assert.Equal((ProcessingState.Failed, "ExternalToolException: ocrmypdf exited with code 8"), (document.State, document.FailureReason));
        Assert.Null(await data.JobAsync(id));
    }

    [Fact]
    public async Task Fail_AfterTheLastAttempt_MarksADocumentFailed_ThatNeverStarted()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "queue_fail_never_started");
        var id = await data.AddQueuedDocumentAsync(s_start);
        await using (var db = data.Context())
        {
            var job = await db.ProcessingJobs.FindAsync([id], Ct);
            job!.Attempt = RetryPolicy.MaxAttempts;
            await db.SaveChangesAsync(Ct);

            var recorded = await data.Queue(db).FailAsync(id, RetryPolicy.MaxAttempts, "NpgsqlException", permanent: false, s_start, Ct);
            Assert.Equal(FailureOutcome.GivenUp, recorded.Outcome);
        }

        Assert.Equal(ProcessingState.Failed, (await data.DocumentAsync(id)).State);
        Assert.Null(await data.JobAsync(id));
    }

    [Fact]
    public async Task Fail_DiscardsUnsavedChangesOfTheFailedAttempt()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "queue_fail_discards");
        var id = await data.AddQueuedDocumentAsync(s_start);
        await using (var db = data.Context())
        {
            var queue = data.Queue(db);
            var job = await queue.ClaimNextAsync(QueueDatabase.LockFor, Ct);
            var document = await queue.StartAsync(id, job!.Attempt, Ct);

            // The attempt broke after changing the tracked document, before saving it.
            document!.Complete("half done");
            await queue.FailAsync(id, job.Attempt, "IOException", permanent: false, s_start, Ct);
        }

        var stored = await data.DocumentAsync(id);
        Assert.Equal(ProcessingState.Processing, stored.State);
        Assert.Null(stored.ContentText);
    }

    [Fact]
    public async Task Fail_ReportsALostClaim_WhenTheJobIsGone()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "queue_fail_gone");
        await using var db = data.Context();

        var recorded = await data.Queue(db).FailAsync(Guid.CreateVersion7(), 1, "IOException", permanent: false, s_start, Ct);

        Assert.Equal(FailureOutcome.ClaimLost, recorded.Outcome);
    }

    [Fact]
    public async Task Release_HandsTheJobBack_WithoutCountingTheAttempt()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "queue_release");
        var id = await data.AddQueuedDocumentAsync(s_start);
        await using var db = data.Context();
        var queue = data.Queue(db);
        var job = await queue.ClaimNextAsync(QueueDatabase.LockFor, Ct);
        await queue.StartAsync(id, job!.Attempt, Ct);

        Assert.True(await queue.ReleaseAsync(id, job.Attempt, Ct));

        var stored = await data.JobAsync(id);
        Assert.Equal((0, (DateTimeOffset?)null), (stored!.Attempt, stored.LockedUntil));
        var again = await queue.ClaimNextAsync(QueueDatabase.LockFor, Ct);
        Assert.Equal((id, 1), (again?.DocumentId, again?.Attempt));
    }
}
