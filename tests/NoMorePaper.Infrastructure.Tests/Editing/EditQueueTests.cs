using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Documents;
using NoMorePaper.Application.Editing;
using NoMorePaper.Application.Processing;
using NoMorePaper.Infrastructure.Editing;
using NoMorePaper.Infrastructure.Tests.Classification;
using NoMorePaper.Infrastructure.Tests.Persistence;
using NoMorePaper.Infrastructure.Tests.Processing;
using NoMorePaper.Infrastructure.Tests.Trash;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Editing;

/// <summary>The queue in M4b: page information with the archive, queuing edits, dropping jobs, and the trash.</summary>
[Collection(SharedPostgres.Name)]
public sealed class EditQueueTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset s_start = QueueDatabase.Start;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Complete_WritesThePages_WithTheArchive()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "edit_pages");
        var id = await data.AddQueuedDocumentAsync(s_start);
        await using var db = data.Context();
        var queue = data.Queue(db);
        var claim = (await queue.ClaimNextAsync(QueueDatabase.LockFor, Ct))!;
        await queue.StartAsync(claim, Ct);

        var pages = new[] { new PageInfo(1, new string('x', 320), false, true), new PageInfo(2, string.Empty, true, false) };
        Assert.True(await queue.CompleteAsync(claim, "Rechnung", new StoredFile(1, QueueDatabase.Sha256), pages, Ct));

        var stored = await new EfDocumentPages(data.Context()).ListAsync(id, Ct);
        Assert.Equal([1, 2], stored.Select(page => page.Page));
        Assert.Equal(PageInfo.MaxHeadLength, stored[0].Head.Length);
        Assert.Equal((false, true), (stored[0].ProbablyBlank, stored[0].HadTextLayer));
        Assert.Equal((true, false), (stored[1].ProbablyBlank, stored[1].HadTextLayer));
        Assert.Empty(await new EfDocumentPages(data.Context()).ListAsync(Guid.CreateVersion7(), Ct));
    }

    [Fact]
    public async Task EnqueueEdit_QueuesAnEditJob_ForADocumentWithoutJob()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "edit_enqueue_free");
        await using var db = data.Context();
        var document = await Seed.DocumentAsync(db, QueueDatabase.Owner, isPrivate: false, text: "Rechnung");
        await PlanAsync(db, document.Id);

        Assert.True(await data.Queue(db).EnqueueEditAsync(document.Id, recognizeAfter: false, Ct));

        var job = (await data.JobAsync(document.Id))!;
        Assert.Equal((JobStage.Edit, 0, 0, s_start), (job.Stage, job.Priority, job.Attempt, job.NextAttemptAt));
        Assert.False((await new EfDocumentEdits(data.Context()).GetAsync(document.Id, Ct))!.RecognizeAfter);
        Assert.Equal(JobStage.Edit, (await data.Queue(data.Context()).ClaimNextAsync(QueueDatabase.LockFor, includeLlmStages: false, Ct))?.Stage);
    }

    [Fact]
    public async Task EnqueueEdit_TakesAWaitingRecognitionOver_SoRecognitionFollows()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "edit_enqueue_takeover");
        var id = await data.AddRecognitionJobAsync(s_start.AddMinutes(5), priority: 10);
        await using var db = data.Context();
        await PlanAsync(db, id);

        Assert.True(await data.Queue(db).EnqueueEditAsync(id, recognizeAfter: false, Ct));

        var job = (await data.JobAsync(id))!;
        Assert.Equal((JobStage.Edit, 0, s_start, (Guid?)null), (job.Stage, job.Priority, job.NextAttemptAt, job.ClaimId));
        Assert.True((await new EfDocumentEdits(data.Context()).GetAsync(id, Ct))!.RecognizeAfter);
    }

    [Fact]
    public async Task EnqueueEdit_IsRefused_WhileAJobRuns_OrOcrWaits()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "edit_enqueue_busy");
        var running = await data.AddRecognitionJobAsync(s_start);
        var ocr = await data.AddQueuedDocumentAsync(s_start.AddHours(1));
        await using var db = data.Context();
        var claim = (await data.Queue(db).ClaimNextAsync(QueueDatabase.LockFor, Ct))!;
        Assert.Equal(running, claim.DocumentId);
        await PlanAsync(db, running);

        Assert.False(await data.Queue(db).EnqueueEditAsync(running, recognizeAfter: true, Ct));
        Assert.False(await data.Queue(db).EnqueueEditAsync(ocr, recognizeAfter: true, Ct));

        var job = (await data.JobAsync(running))!;
        Assert.Equal((JobStage.Recognition, claim.ClaimId), (job.Stage, job.ClaimId));
        Assert.False((await new EfDocumentEdits(data.Context()).GetAsync(running, Ct))!.RecognizeAfter);
        Assert.Equal(JobStage.Ocr, (await data.JobAsync(ocr))!.Stage);
    }

    [Fact]
    public async Task Edits_AreSavedReplacedAndDeleted_AndCancelDropsTheJob()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "edit_rows");
        var id = await data.AddQueuedDocumentAsync(s_start);
        var edits = new EfDocumentEdits(data.Context());
        await edits.SaveAsync(new DocumentEdit(id, EditKind.Pages, """{"steps":[]}""", QueueDatabase.Owner, s_start, false), Ct);
        await edits.SaveAsync(new DocumentEdit(id, EditKind.Rebuild, "{}", QueueDatabase.Owner, s_start.AddHours(1), true), Ct);

        var edit = (await new EfDocumentEdits(data.Context()).GetAsync(id, Ct))!;
        Assert.Equal((EditKind.Rebuild, "{}", s_start.AddHours(1), true), (edit.Kind, edit.Data, edit.RequestedAt, edit.RecognizeAfter));

        await edits.DeleteAsync(id, Ct);
        Assert.Null(await edits.GetAsync(id, Ct));
        Assert.True(await data.Queue(data.Context()).CancelAsync(id, Ct));
        Assert.False(await data.Queue(data.Context()).CancelAsync(id, Ct));
    }

    [Fact]
    public async Task TheQueue_LeavesDocumentsInTheTrashAlone()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "edit_trash_queue");
        await using var db = data.Context();
        var trashed = await Seed.DocumentAsync(db, QueueDatabase.Owner, isPrivate: false, text: "Rechnung");
        await Trashed.MoveAsync(db, trashed);
        await db.Database.ExecuteSqlAsync(
            $"INSERT INTO processing_jobs (document_id, stage, priority, attempt, next_attempt_at) VALUES ({trashed.Id}, 'Ocr', 0, 0, {s_start})", Ct);
        var queue = data.Queue(db);

        Assert.Null(await queue.ClaimNextAsync(QueueDatabase.LockFor, Ct));
        await queue.CancelAsync(trashed.Id, Ct);
        Assert.Equal(0, await queue.EnqueueRecognitionBacklogAsync(10, s_start, Ct));
        Assert.False(await queue.RequeueRecognitionAsync(trashed.Id, Ct));
        Assert.Null(await data.JobAsync(trashed.Id));
    }

    private static Task PlanAsync(Infrastructure.Persistence.AppDbContext db, Guid id) =>
        new EfDocumentEdits(db).SaveAsync(new DocumentEdit(id, EditKind.Pages, "{}", QueueDatabase.Owner, s_start, false), Ct);
}
