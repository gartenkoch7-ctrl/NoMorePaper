using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Processing;
using NoMorePaper.Infrastructure.Tests.Classification;
using NoMorePaper.Infrastructure.Tests.Persistence;
using NoMorePaper.Infrastructure.Tests.Processing;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Recognition;

[Collection(SharedPostgres.Name)]
public sealed class RecognitionBacklogTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset s_start = QueueDatabase.Start;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EnqueueBacklog_QueuesDoneDocumentsWithTextAndWithoutFields_Once()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "recognition_backlog");
        var owner = Guid.CreateVersion7();
        await using var db = data.Context();
        var shared = await Seed.DocumentAsync(db, owner, isPrivate: false, text: "Rechnung");
        var hidden = await Seed.DocumentAsync(db, owner, isPrivate: true, text: "Arztbrief");
        await Seed.DocumentAsync(db, owner, isPrivate: false, fields: Seed.Fields(), text: "schon erkannt");
        await Seed.DocumentAsync(db, owner, isPrivate: false, text: " \n\t ");
        await Seed.DocumentAsync(db, owner, isPrivate: false);
        var queued = await data.AddQueuedDocumentAsync(s_start);
        var waiting = await data.AddRecognitionJobAsync(s_start.AddMinutes(5));

        var count = await data.Queue(db).EnqueueRecognitionBacklogAsync(10, s_start, Ct);

        Assert.Equal(2, count);
        var jobs = await db.ProcessingJobs.AsNoTracking().ToListAsync(Ct);
        Assert.Equal(
            [shared.Id, hidden.Id],
            jobs.Where(job => job.Priority == 10).Select(job => job.DocumentId).Order().ToArray());
        Assert.All(jobs.Where(job => job.Priority == 10), job =>
            Assert.Equal((JobStage.Recognition, 0, s_start), (job.Stage, job.Attempt, job.NextAttemptAt)));
        Assert.Equal(JobStage.Ocr, jobs.Single(job => job.DocumentId == queued).Stage);
        Assert.Equal(s_start.AddMinutes(5), jobs.Single(job => job.DocumentId == waiting).NextAttemptAt);

        Assert.Equal(0, await data.Queue(db).EnqueueRecognitionBacklogAsync(10, s_start, Ct));
    }
}
