using NoMorePaper.Application.Documents;
using NoMorePaper.Application.Processing;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Processing;
using NoMorePaper.Infrastructure.Tests.Persistence;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Processing;

/// <summary>
/// A Worker whose lock expired while it was still working, and whose job another Worker claimed
/// since: its later calls must change nothing, while the new owner works normally.
/// </summary>
[Collection(SharedPostgres.Name)]
public sealed class JobQueueTakeoverTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset s_start = QueueDatabase.Start;
    private static readonly DateTimeOffset s_takeover = s_start + QueueDatabase.LockFor + TimeSpan.FromSeconds(1);
    private static readonly StoredFile s_archive = new(4711, QueueDatabase.Sha256);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheFormerOwner_CanNeitherStartCompleteFailNorRelease()
    {
        var (data, stale, owner) = await TakenOverAsync("queue_takeover_stale");
        await using var db = data.Context();
        var queue = data.Queue(db);

        Assert.Null(await queue.StartAsync(stale, Ct));
        Assert.False(await queue.CompleteAsync(stale, "stale text", s_archive, Ct));
        Assert.Equal(new RecordedFailure(FailureOutcome.ClaimLost), await queue.FailAsync(stale, "IOException", permanent: true, s_start, Ct));
        Assert.False(await queue.ReleaseAsync(stale, Ct));

        var job = await data.JobAsync(owner.DocumentId);
        Assert.Equal((2, owner.ClaimId, s_takeover + QueueDatabase.LockFor), (job!.Attempt, job.ClaimId, job.LockedUntil));
        Assert.Null(job.LastError);
        var document = await data.DocumentAsync(owner.DocumentId);
        Assert.Equal(ProcessingState.Processing, document.State);
        Assert.Null(document.ContentText);
        Assert.Null(document.FindFile(DocumentFileKind.Archive));
    }

    [Fact]
    public async Task TheNewOwner_CompletesTheDocument()
    {
        var (data, _, owner) = await TakenOverAsync("queue_takeover_new_owner");
        await using var db = data.Context();
        var queue = data.Queue(db);

        Assert.NotNull(await queue.StartAsync(owner, Ct));
        Assert.True(await queue.CompleteAsync(owner, "Rechnung", s_archive, Ct));

        Assert.Equal(ProcessingState.Done, (await data.DocumentAsync(owner.DocumentId)).State);
        Assert.Null(await data.JobAsync(owner.DocumentId));
    }

    [Fact]
    public async Task TheFormerOwner_CannotComplete_AfterTheNewOwnerFailed()
    {
        var (data, stale, owner) = await TakenOverAsync("queue_takeover_after_fail");
        await using (var db = data.Context())
        {
            var queue = data.Queue(db);
            await queue.StartAsync(owner, Ct);
            await queue.FailAsync(owner, "IOException", permanent: false, data.Time.Now, Ct);
        }

        await using (var db = data.Context())
        {
            Assert.False(await data.Queue(db).CompleteAsync(stale, "stale text", s_archive, Ct));
        }

        var job = await data.JobAsync(owner.DocumentId);
        Assert.Equal((2, "IOException", (Guid?)null), (job!.Attempt, job.LastError, job.ClaimId));
        Assert.Equal(ProcessingState.Processing, (await data.DocumentAsync(owner.DocumentId)).State);
    }

    [Fact]
    public async Task TheFormerOwner_CannotComplete_AfterTheNewOwnerReleasedAndAThirdClaimed()
    {
        // B hands its attempt back, so the job is at attempt 1 again (A's number) and C counts
        // attempt 2 again (B's number): attempt numbers repeat, claim ids do not.
        var (data, stale, owner) = await TakenOverAsync("queue_takeover_after_release");
        JobClaim third;
        await using (var db = data.Context())
        {
            var queue = data.Queue(db);
            Assert.True(await queue.ReleaseAsync(owner, Ct));
            Assert.Equal(stale.Attempt, (await data.JobAsync(stale.DocumentId))!.Attempt);
            Assert.Null(await queue.StartAsync(stale, Ct));
            third = (await queue.ClaimNextAsync(QueueDatabase.LockFor, Ct))!;
        }

        Assert.Equal(owner.Attempt, third.Attempt);
        Assert.NotEqual(owner.ClaimId, third.ClaimId);
        await using (var db = data.Context())
        {
            var queue = data.Queue(db);
            Assert.Null(await queue.StartAsync(stale, Ct));
            Assert.False(await queue.CompleteAsync(stale, "stale text", s_archive, Ct));
            Assert.Equal(new RecordedFailure(FailureOutcome.ClaimLost), await queue.FailAsync(stale, "IOException", permanent: true, s_start, Ct));
            Assert.False(await queue.ReleaseAsync(stale, Ct));
            Assert.Null(await queue.StartAsync(owner, Ct));
            Assert.False(await queue.ReleaseAsync(owner, Ct));
        }

        var job = await data.JobAsync(third.DocumentId);
        Assert.Equal((2, third.ClaimId), (job!.Attempt, job.ClaimId));
        Assert.Null((await data.DocumentAsync(third.DocumentId)).ContentText);
        await using (var db = data.Context())
        {
            Assert.True(await data.Queue(db).CompleteAsync(third, "Rechnung", s_archive, Ct));
        }
    }

    // Worker A claims and starts; its lock expires; Worker B claims the job as attempt 2.
    private async Task<(QueueDatabase Data, JobClaim Stale, JobClaim Owner)> TakenOverAsync(string database)
    {
        var data = await QueueDatabase.CreateAsync(postgres, database);
        await data.AddQueuedDocumentAsync(s_start);
        JobClaim stale;
        await using (var first = data.Context())
        {
            var queue = data.Queue(first);
            stale = (await queue.ClaimNextAsync(QueueDatabase.LockFor, Ct))!;
            Assert.Equal(1, stale.Attempt);
            Assert.NotNull(await queue.StartAsync(stale, Ct));
        }

        data.Time.Now = s_takeover;
        JobClaim owner;
        await using (var second = data.Context())
        {
            owner = (await data.Queue(second).ClaimNextAsync(QueueDatabase.LockFor, Ct))!;
        }

        Assert.Equal((stale.DocumentId, 2), (owner.DocumentId, owner.Attempt));
        return (data, stale, owner);
    }
}
