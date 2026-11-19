using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Processing;
using NoMorePaper.Application.Recognition;
using NoMorePaper.Application.Documents;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Tests.Persistence;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Processing;

[Collection(SharedPostgres.Name)]
public sealed class JobQueueRecognitionTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset s_start = QueueDatabase.Start;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DocumentFields Fields => new(
        "Stromrechnung November 2026", null, null, new DateOnly(2026, 11, 1), new Money(84.2m, "EUR"), "Abschlag für November.", []);

    [Fact]
    public async Task ClaimNext_PrefersTheLowerPriority_ThenTheEarlierJob()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "queue_recognition_priority");
        var backlog = await data.AddRecognitionJobAsync(s_start.AddHours(-1), priority: 10);
        var later = await data.AddQueuedDocumentAsync(s_start.AddMinutes(-1));
        var earlier = await data.AddRecognitionJobAsync(s_start.AddMinutes(-2));
        await using var db = data.Context();
        var queue = data.Queue(db);

        var first = await queue.ClaimNextAsync(QueueDatabase.LockFor, Ct);
        var second = await queue.ClaimNextAsync(QueueDatabase.LockFor, Ct);
        var third = await queue.ClaimNextAsync(QueueDatabase.LockFor, Ct);

        Assert.Equal((earlier, JobStage.Recognition), (first?.DocumentId, first?.Stage));
        Assert.Equal((later, JobStage.Ocr), (second?.DocumentId, second?.Stage));
        Assert.Equal(backlog, third?.DocumentId);
    }

    [Fact]
    public async Task Complete_RemovesTheJob_WhenThereIsNoTextToRecognize()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "queue_complete_no_text");
        var id = await data.AddQueuedDocumentAsync(s_start);
        await using var db = data.Context();
        var queue = data.Queue(db);
        var claim = (await queue.ClaimNextAsync(QueueDatabase.LockFor, Ct))!;
        await queue.StartAsync(claim, Ct);

        Assert.True(await queue.CompleteAsync(claim, " \n", new StoredFile(1, QueueDatabase.Sha256), Ct));

        Assert.Equal(ProcessingState.Done, (await data.DocumentAsync(id)).State);
        Assert.Null(await data.JobAsync(id));
    }

    [Fact]
    public async Task ClaimNext_LeavesRecognitionJobs_WhileTheModelIsNotReady()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "queue_recognition_not_ready");
        await data.AddRecognitionJobAsync(s_start.AddMinutes(-2));
        var upload = await data.AddQueuedDocumentAsync(s_start);
        await using var db = data.Context();
        var queue = data.Queue(db);

        Assert.Equal(upload, (await queue.ClaimNextAsync(QueueDatabase.LockFor, includeRecognition: false, Ct))?.DocumentId);
        Assert.Null(await queue.ClaimNextAsync(QueueDatabase.LockFor, includeRecognition: false, Ct));
        Assert.Equal(JobStage.Recognition, (await queue.ClaimNextAsync(QueueDatabase.LockFor, includeRecognition: true, Ct))?.Stage);
    }

    [Fact]
    public async Task Fail_AtRecognition_SchedulesARetry_AndGivingUp_RemovesOnlyTheJob()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "queue_recognition_fail");
        var id = await data.AddRecognitionJobAsync(s_start);
        await using var db = data.Context();
        var queue = data.Queue(db);

        var first = (await queue.ClaimNextAsync(QueueDatabase.LockFor, Ct))!;
        var retry = await queue.FailAsync(first, "RecognitionException: LLM service answered 500", permanent: false, s_start, Ct);
        Assert.Equal((FailureOutcome.RetryScheduled, s_start.AddMinutes(1)), (retry.Outcome, retry.NextAttemptAt));
        Assert.Equal(JobStage.Recognition, (await data.JobAsync(id))!.Stage);

        data.Time.Now = s_start.AddMinutes(1);
        var second = (await queue.ClaimNextAsync(QueueDatabase.LockFor, Ct))!;
        var givenUp = await queue.FailAsync(second, "TimeoutException", permanent: true, data.Time.Now, Ct);

        Assert.Equal(FailureOutcome.GivenUp, givenUp.Outcome);
        Assert.Null(await data.JobAsync(id));
        var document = await data.DocumentAsync(id);
        Assert.Equal((ProcessingState.Done, null, RecognitionState.None), (document.State, document.FailureReason, document.Recognition));
    }

    [Fact]
    public async Task Read_ReturnsTheDocument_OnlyWhileTheClaimHolds()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "queue_recognition_read");
        var id = await data.AddRecognitionJobAsync(s_start);
        await using var db = data.Context();
        var queue = data.Queue(db);
        var claim = (await queue.ClaimNextAsync(QueueDatabase.LockFor, Ct))!;

        Assert.Equal("Rechnung", (await queue.ReadAsync(claim, Ct))?.ContentText);
        Assert.True(await queue.ReleaseAsync(claim, Ct));
        Assert.Null(await queue.ReadAsync(claim, Ct));
        Assert.Equal(ProcessingState.Done, (await data.DocumentAsync(id)).State);
    }

    [Fact]
    public async Task CompleteRecognition_ClassifiesTheDocument_AndRemovesTheJob()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "queue_recognition_complete");
        var id = await data.AddRecognitionJobAsync(s_start);
        await using var db = data.Context();
        var queue = data.Queue(db);
        var claim = (await queue.ClaimNextAsync(QueueDatabase.LockFor, Ct))!;

        Assert.Equal(RecognitionCommit.Classified, await queue.CompleteRecognitionAsync(claim, Fields, wasPrivate: false, Ct));

        var document = await data.DocumentAsync(id);
        Assert.Equal((RecognitionState.Automatic, "Stromrechnung November 2026"), (document.Recognition, document.Title));
        Assert.Equal((new DateOnly(2026, 11, 1), 84.2m), (document.DocumentDate, document.Amount?.Value));
        Assert.Null(await data.JobAsync(id));
    }

    [Fact]
    public async Task CompleteRecognition_KeepsFieldsConfirmedDuringRecognition()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "queue_recognition_confirmed");
        var id = await data.AddRecognitionJobAsync(s_start);
        await using var db = data.Context();
        var queue = data.Queue(db);
        var claim = (await queue.ClaimNextAsync(QueueDatabase.LockFor, Ct))!;
        await queue.ReadAsync(claim, Ct);

        // The owner confirms in the app while the model is still working.
        await using (var app = data.Context())
        {
            (await app.Documents.SingleAsync(document => document.Id == id, Ct)).Confirm(QueueDatabase.Owner);
            await app.SaveChangesAsync(Ct);
        }

        Assert.Equal(RecognitionCommit.JobRemoved, await queue.CompleteRecognitionAsync(claim, Fields, wasPrivate: false, Ct));

        var stored = await data.DocumentAsync(id);
        Assert.Equal((RecognitionState.Confirmed, "Scan 1", null), (stored.Recognition, stored.Title, stored.Summary));
        Assert.Null(await data.JobAsync(id));
    }

    [Fact]
    public async Task CompleteRecognition_WithoutFields_OnlyRemovesTheJob()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "queue_recognition_no_fields");
        var id = await data.AddRecognitionJobAsync(s_start);
        await using var db = data.Context();
        var queue = data.Queue(db);
        var claim = (await queue.ClaimNextAsync(QueueDatabase.LockFor, Ct))!;

        Assert.Equal(RecognitionCommit.JobRemoved, await queue.CompleteRecognitionAsync(claim, fields: null, wasPrivate: false, Ct));

        Assert.Equal(RecognitionState.None, (await data.DocumentAsync(id)).Recognition);
        Assert.Null(await data.JobAsync(id));
    }

    [Fact]
    public async Task CompleteRecognition_ChangesNothing_WhenTheClaimIsLost()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "queue_recognition_claim_lost");
        var id = await data.AddRecognitionJobAsync(s_start);
        await using var db = data.Context();
        var queue = data.Queue(db);
        var stale = (await queue.ClaimNextAsync(QueueDatabase.LockFor, Ct))!;
        data.Time.Now = s_start + QueueDatabase.LockFor + TimeSpan.FromSeconds(1);
        await using var other = data.Context();
        var current = (await data.Queue(other).ClaimNextAsync(QueueDatabase.LockFor, Ct))!;

        Assert.Equal(RecognitionCommit.ClaimLost, await queue.CompleteRecognitionAsync(stale, Fields, wasPrivate: false, Ct));

        Assert.Equal(RecognitionState.None, (await data.DocumentAsync(id)).Recognition);
        Assert.Equal(current.ClaimId, (await data.JobAsync(id))!.ClaimId);
    }

    [Fact]
    public async Task Claim_CountsAttemptsOfTheRecognitionStageOnly()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "queue_recognition_attempts");
        await data.AddQueuedDocumentAsync(s_start);

        // A context per claim, like the Worker's scope per job.
        await using (var db = data.Context())
        {
            var queue = data.Queue(db);
            var ocr = (await queue.ClaimNextAsync(QueueDatabase.LockFor, Ct))!;
            await queue.FailAsync(ocr, "TimeoutException", permanent: false, s_start, Ct);
        }

        data.Time.Now = s_start.AddMinutes(1);
        JobClaim second;
        await using (var db = data.Context())
        {
            var queue = data.Queue(db);
            second = (await queue.ClaimNextAsync(QueueDatabase.LockFor, Ct))!;
            await queue.StartAsync(second, Ct);
            Assert.True(await queue.CompleteAsync(second, "Rechnung", new StoredFile(1, QueueDatabase.Sha256), Ct));
        }

        await using var next = data.Context();
        var recognition = await data.Queue(next).ClaimNextAsync(QueueDatabase.LockFor, Ct);

        Assert.Equal((2, JobStage.Recognition, 1), (second.Attempt, recognition?.Stage, recognition?.Attempt));
    }
}
