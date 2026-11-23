using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Documents;
using NoMorePaper.Application.Processing;
using NoMorePaper.Infrastructure.Processing;
using NoMorePaper.Infrastructure.Tests.Persistence;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Processing;

/// <summary>
/// Moving a document to the trash locks the document, then removes its job. The Worker's steps lock in the
/// same order, so a step that runs meanwhile waits for the trash and then finds its claim lost, instead of
/// holding the job while the trash waits for it (a deadlock).
/// </summary>
[Collection(SharedPostgres.Name)]
public sealed class JobQueueLockOrderTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset s_start = QueueDatabase.Start;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("start")]
    [InlineData("complete")]
    [InlineData("fail")]
    public async Task WorkerStep_WaitsForTheTrash_AndReportsTheClaimLost(string step)
    {
        var data = await QueueDatabase.CreateAsync(postgres, $"queue_lock_order_{step}");
        var id = await data.AddQueuedDocumentAsync(s_start);
        await using var worker = data.Context();
        var queue = data.Queue(worker);
        var claim = (await queue.ClaimNextAsync(QueueDatabase.LockFor, Ct))!;
        if (step != "start")
        {
            Assert.NotNull(await queue.StartAsync(claim, Ct));
        }

        await using var trash = data.Context();
        await using var change = await trash.Database.BeginTransactionAsync(Ct);
        await trash.Database.ExecuteSqlAsync($"SELECT 1 FROM documents WHERE id = {id} FOR NO KEY UPDATE", Ct);
        var document = await trash.Documents.SingleAsync(found => found.Id == id, Ct);
        document.MoveToTrash(QueueDatabase.Owner, s_start);

        var running = RunStepAsync(queue, claim, step);
        await Task.Delay(TimeSpan.FromMilliseconds(300), Ct);
        Assert.False(running.IsCompleted);

        // Without the order the Worker would hold the job here, and removing it would deadlock.
        Assert.True(await data.Queue(trash).CancelAsync(id, Ct));
        await trash.SaveChangesAsync(Ct);
        await change.CommitAsync(Ct);

        Assert.False(await running.WaitAsync(TimeSpan.FromSeconds(30), Ct));
        Assert.Null(await data.JobAsync(id));
    }

    [Theory]
    [InlineData("start")]
    [InlineData("complete")]
    [InlineData("fail")]
    public async Task WorkerStep_StillWorks_WhenNothingElseHoldsTheDocument(string step)
    {
        var data = await QueueDatabase.CreateAsync(postgres, $"queue_lock_order_alone_{step}");
        await data.AddQueuedDocumentAsync(s_start);
        await using var worker = data.Context();
        var queue = data.Queue(worker);
        var claim = (await queue.ClaimNextAsync(QueueDatabase.LockFor, Ct))!;
        if (step != "start")
        {
            Assert.NotNull(await queue.StartAsync(claim, Ct));
        }

        Assert.True(await RunStepAsync(queue, claim, step));
    }

    // Whether the step acted on its claim.
    private static async Task<bool> RunStepAsync(JobQueue queue, JobClaim claim, string step) => step switch
    {
        "start" => await queue.StartAsync(claim, Ct) is not null,
        "complete" => await queue.CompleteAsync(claim, "Rechnung", new StoredFile(4711, QueueDatabase.Sha256), [], Ct),
        _ => (await queue.FailAsync(claim, "TimeoutException", permanent: false, s_start, Ct)).Outcome != FailureOutcome.ClaimLost,
    };
}
