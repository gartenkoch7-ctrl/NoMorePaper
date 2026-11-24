using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Editing;
using NoMorePaper.Application.Processing;
using NoMorePaper.Infrastructure.Editing;
using NoMorePaper.Infrastructure.Tests.Persistence;
using NoMorePaper.Infrastructure.Tests.Processing;
using NoMorePaper.Infrastructure.Tests.Trash;
using Xunit;
using static NoMorePaper.Infrastructure.Tests.Editing.EditStageData;

namespace NoMorePaper.Infrastructure.Tests.Editing;

/// <summary>The page backlog (M4b, F1): jobs at the edit stage without planned edit.</summary>
[Collection(SharedPostgres.Name)]
public sealed class PageBacklogQueueTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheBacklog_QueuesDoneDocumentsWithoutPages_Once()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "page_backlog_queue");
        var old = await DoneAsync(data);
        var withPages = await DoneAsync(data);
        var trashed = await DoneAsync(data);
        var queued = await data.AddQueuedDocumentAsync(Start.AddDays(1));
        await using var db = data.Context();
        await db.Database.ExecuteSqlAsync(
            $"INSERT INTO document_pages (document_id, page, head, probably_blank, had_text_layer) VALUES ({withPages.Id}, 1, '', false, false)", Ct);
        db.Attach(trashed);
        await Trashed.MoveAsync(db, trashed);
        var queue = data.Queue(db);

        Assert.Equal(1, await queue.EnqueuePageBacklogAsync(20, Start, Ct));
        Assert.Equal(0, await queue.EnqueuePageBacklogAsync(20, Start, Ct));

        var job = (await data.JobAsync(old.Id))!;
        Assert.Equal((JobStage.Edit, 20), (job.Stage, job.Priority));
        Assert.Null(await EditAsync(data, old.Id));
        Assert.Equal(JobStage.Ocr, (await data.JobAsync(queued))!.Stage);
    }

    [Fact]
    public async Task CompleteAnalysis_WritesThePages_AndRemovesTheJob()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "page_backlog_complete");
        var document = await DoneAsync(data);
        await using var db = data.Context();
        var queue = data.Queue(db);
        await queue.EnqueuePageBacklogAsync(20, Start, Ct);
        var claim = (await queue.ClaimNextAsync(QueueDatabase.LockFor, Ct))!;

        Assert.True(await queue.CompleteAnalysisAsync(claim, [new PageInfo(1, "Sehr geehrte", false, true)], Ct));
        Assert.False(await queue.CompleteAnalysisAsync(claim, [], Ct));

        Assert.Null(await data.JobAsync(document.Id));
        Assert.Equal("Sehr geehrte", (await new EfDocumentPages(data.Context()).ListAsync(document.Id, Ct)).Single().Head);
    }

    [Fact]
    public async Task AWaitingAnalysis_GivesWayToAnEdit_ButNotARunningOne_NorAPlannedEdit()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "page_backlog_drop");
        var running = await DoneAsync(data);
        await using var db = data.Context();
        var queue = data.Queue(db);
        await queue.EnqueuePageBacklogAsync(20, Start, Ct);
        Assert.Equal(running.Id, (await queue.ClaimNextAsync(QueueDatabase.LockFor, Ct))!.DocumentId);
        var waiting = await DoneAsync(data);
        var planned = await DoneAsync(data);
        await queue.EnqueuePageBacklogAsync(20, Start, Ct);
        await new EfDocumentEdits(db).SaveAsync(new DocumentEdit(planned.Id, EditKind.Pages, "{}", QueueDatabase.Owner, Start, false), Ct);

        Assert.False(await queue.DropPendingAnalysisAsync(running.Id, Ct));
        Assert.True(await queue.DropPendingAnalysisAsync(waiting.Id, Ct));
        Assert.False(await queue.DropPendingAnalysisAsync(planned.Id, Ct));

        Assert.Null(await data.JobAsync(waiting.Id));
        Assert.NotNull(await data.JobAsync(running.Id));
        Assert.NotNull(await data.JobAsync(planned.Id));
    }
}
