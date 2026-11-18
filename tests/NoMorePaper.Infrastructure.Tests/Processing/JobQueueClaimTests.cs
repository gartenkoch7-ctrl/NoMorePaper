using System.Collections.Concurrent;
using NoMorePaper.Infrastructure.Tests.Persistence;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Processing;

[Collection(SharedPostgres.Name)]
public sealed class JobQueueClaimTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset s_start = QueueDatabase.Start;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ClaimNext_ReturnsNull_WhenNoJobIsDue()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "queue_claim_none_due");
        await data.AddQueuedDocumentAsync(s_start.AddSeconds(1));
        await using var db = data.Context();

        Assert.Null(await data.Queue(db).ClaimNextAsync(QueueDatabase.LockFor, Ct));
    }

    [Fact]
    public async Task ClaimNext_TakesTheEarliestDueJob_CountsTheAttempt_AndLocksIt()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "queue_claim_earliest");
        await data.AddQueuedDocumentAsync(s_start.AddMinutes(-1));
        var earliest = await data.AddQueuedDocumentAsync(s_start.AddMinutes(-5));
        await using var db = data.Context();

        var job = await data.Queue(db).ClaimNextAsync(QueueDatabase.LockFor, Ct);

        Assert.NotNull(job);
        Assert.Equal(earliest, job.DocumentId);
        Assert.Equal(1, job.Attempt);
        var stored = await data.JobAsync(earliest);
        Assert.Equal((s_start + QueueDatabase.LockFor, job.ClaimId), (stored!.LockedUntil, stored.ClaimId));
    }

    [Fact]
    public async Task ClaimNext_SkipsAClaimedJob_UntilItsLockHasExpired()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "queue_claim_lock_expiry");
        var id = await data.AddQueuedDocumentAsync(s_start);
        await using var db = data.Context();
        var queue = data.Queue(db);

        var first = await queue.ClaimNextAsync(QueueDatabase.LockFor, Ct);
        Assert.NotNull(first);
        Assert.Null(await queue.ClaimNextAsync(QueueDatabase.LockFor, Ct));

        // The Worker holding it crashed: once the lock has expired, another one takes over.
        data.Time.Now = s_start + QueueDatabase.LockFor + TimeSpan.FromSeconds(1);
        var retaken = await queue.ClaimNextAsync(QueueDatabase.LockFor, Ct);

        Assert.Equal((id, 2), (retaken?.DocumentId, retaken?.Attempt));
        Assert.NotEqual(first.ClaimId, retaken?.ClaimId);
    }

    [Fact]
    public async Task ClaimNext_InParallel_NeverHandsOutAJobTwice()
    {
        const int jobs = 20;
        const int workers = 4;
        var data = await QueueDatabase.CreateAsync(postgres, "queue_claim_parallel");
        for (var i = 0; i < jobs; i++)
        {
            await data.AddQueuedDocumentAsync(s_start.AddSeconds(-i));
        }

        var claimed = new ConcurrentBag<Guid>();
        await Task.WhenAll(Enumerable.Range(0, workers).Select(_ => Task.Run(
            async () =>
            {
                // Each Worker has its own connection, as separate processes would.
                await using var db = data.Context();
                var queue = data.Queue(db);
                while (await queue.ClaimNextAsync(QueueDatabase.LockFor, Ct) is { } job)
                {
                    claimed.Add(job.DocumentId);
                }
            },
            Ct)));

        Assert.Equal(jobs, claimed.Count);
        Assert.Equal(jobs, claimed.Distinct().Count());
    }

    [Fact]
    public async Task ClaimNext_InParallel_ForASingleJob_GivesItToExactlyOneWorker()
    {
        const int workers = 8;
        var data = await QueueDatabase.CreateAsync(postgres, "queue_claim_single");
        var id = await data.AddQueuedDocumentAsync(s_start);

        var results = await Task.WhenAll(Enumerable.Range(0, workers).Select(_ => Task.Run(
            async () =>
            {
                await using var db = data.Context();
                return await data.Queue(db).ClaimNextAsync(QueueDatabase.LockFor, Ct);
            },
            Ct)));

        var winner = Assert.Single(results, job => job is not null);
        Assert.Equal(id, winner!.DocumentId);
        Assert.Equal(1, (await data.JobAsync(id))!.Attempt);
    }
}
