using NoMorePaper.Application.Documents;
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
    private static readonly StoredFile s_archive = new(4711, QueueDatabase.Sha256);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheFormerOwner_CanNeitherStartCompleteFailNorRelease()
    {
        var (data, id) = await TakenOverAsync("queue_takeover_stale");
        await using var db = data.Context();
        var stale = data.Queue(db);

        Assert.Null(await stale.StartAsync(id, 1, Ct));
        Assert.False(await stale.CompleteAsync(id, 1, "stale text", s_archive, Ct));
        Assert.Equal(new RecordedFailure(FailureOutcome.ClaimLost), await stale.FailAsync(id, 1, "IOException", permanent: true, s_start, Ct));
        Assert.False(await stale.ReleaseAsync(id, 1, Ct));

        var job = await data.JobAsync(id);
        Assert.Equal((2, s_start + QueueDatabase.LockFor + TimeSpan.FromSeconds(1) + QueueDatabase.LockFor), (job!.Attempt, job.LockedUntil));
        Assert.Null(job.LastError);
        var document = await data.DocumentAsync(id);
        Assert.Equal(ProcessingState.Processing, document.State);
        Assert.Null(document.ContentText);
        Assert.Null(document.FindFile(DocumentFileKind.Archive));
    }

    [Fact]
    public async Task TheNewOwner_CompletesTheDocument()
    {
        var (data, id) = await TakenOverAsync("queue_takeover_new_owner");
        await using var db = data.Context();
        var owner = data.Queue(db);

        Assert.NotNull(await owner.StartAsync(id, 2, Ct));
        Assert.True(await owner.CompleteAsync(id, 2, "Rechnung", s_archive, Ct));

        Assert.Equal(ProcessingState.Done, (await data.DocumentAsync(id)).State);
        Assert.Null(await data.JobAsync(id));
    }

    [Fact]
    public async Task TheFormerOwner_CannotComplete_AfterTheNewOwnerFailed()
    {
        var (data, id) = await TakenOverAsync("queue_takeover_after_fail");
        await using (var db = data.Context())
        {
            var owner = data.Queue(db);
            await owner.StartAsync(id, 2, Ct);
            await owner.FailAsync(id, 2, "IOException", permanent: false, data.Time.Now, Ct);
        }

        await using (var db = data.Context())
        {
            Assert.False(await data.Queue(db).CompleteAsync(id, 1, "stale text", s_archive, Ct));
        }

        var job = await data.JobAsync(id);
        Assert.Equal((2, "IOException"), (job!.Attempt, job.LastError));
        Assert.Equal(ProcessingState.Processing, (await data.DocumentAsync(id)).State);
    }

    // Worker 1 claims and starts; its lock expires; Worker 2 claims the job as attempt 2.
    private async Task<(QueueDatabase Data, Guid Id)> TakenOverAsync(string database)
    {
        var data = await QueueDatabase.CreateAsync(postgres, database);
        var id = await data.AddQueuedDocumentAsync(s_start);
        await using (var first = data.Context())
        {
            var queue = data.Queue(first);
            Assert.Equal(1, (await queue.ClaimNextAsync(QueueDatabase.LockFor, Ct))?.Attempt);
            Assert.NotNull(await queue.StartAsync(id, 1, Ct));
        }

        data.Time.Now = s_start + QueueDatabase.LockFor + TimeSpan.FromSeconds(1);
        await using (var second = data.Context())
        {
            Assert.Equal(2, (await data.Queue(second).ClaimNextAsync(QueueDatabase.LockFor, Ct))?.Attempt);
        }

        return (data, id);
    }
}
