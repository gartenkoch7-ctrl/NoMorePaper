using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Editing;
using NoMorePaper.Application.Processing;
using NoMorePaper.Application.Trash;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Domain.Processing;
using NoMorePaper.Infrastructure.Documents;
using NoMorePaper.Infrastructure.Editing;
using NoMorePaper.Infrastructure.Processing;
using NoMorePaper.Infrastructure.Tests.Persistence;
using NoMorePaper.Infrastructure.Tests.Processing;
using NoMorePaper.Infrastructure.Trash;
using Xunit;
using static NoMorePaper.Infrastructure.Tests.Editing.EditStageData;

namespace NoMorePaper.Infrastructure.Tests.Editing;

/// <summary>The edit stage of the queue (M4b P1): storing a version, finishing it, and giving it up.</summary>
[Collection(SharedPostgres.Name)]
public sealed class EditCommitTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CompleteEdit_StoresTheVersion_KeepsTheJob_AndFinish_HandsItToRecognition()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "edit_commit_version");
        var document = await DoneAsync(data);
        var claim = await ClaimedEditAsync(data, document.Id, recognizeAfter: true);
        var item = TrashItem.ForFile(document, document.FindFile(DocumentFileKind.Original)!, Start);
        var completion = new EditCompletion(
            "Neuer Text", [new PageInfo(1, "Neu", false, false), new PageInfo(2, string.Empty, true, false)], null, [item],
            """{"editId":"00000000-0000-7000-8000-000000000000","finish":[]}""", Final: false,
            (tracked, other) =>
            {
                Assert.Null(other);
                tracked.ReplaceFile(File(DocumentFileKind.Edited, 'c'), Start.AddHours(1));
                tracked.ReplaceFile(File(DocumentFileKind.Archive, 'd'), Start.AddHours(1));
                return true;
            });

        await using (var db = data.Context())
        {
            Assert.Equal(EditCommitResult.Committed, await data.Queue(db).CompleteEditAsync(claim, completion, Ct));
        }

        var stored = await data.DocumentAsync(document.Id);
        Assert.Equal(("Neuer Text", Sha('c'), Sha('d'), Sha('a')), (stored.ContentText, stored.FindFile(DocumentFileKind.Edited)!.Sha256,
            stored.FindFile(DocumentFileKind.Archive)!.Sha256, stored.FindFile(DocumentFileKind.Original)!.Sha256));
        Assert.Equal(Start.AddHours(1), stored.EditedAt);
        Assert.Equal(2, (await new EfDocumentPages(data.Context()).ListAsync(document.Id, Ct)).Count);
        Assert.Contains("\"finish\"", (await EditAsync(data, document.Id))!.Data, StringComparison.Ordinal);
        Assert.Equal(claim.ClaimId, (await data.JobAsync(document.Id))!.ClaimId);
        await using (var db = data.Context())
        {
            Assert.Equal(item.Id, (await new EfTrashStore(db).ListAsync(QueueDatabase.Owner, Ct)).Single().Item.Id);
            Assert.True(await data.Queue(db).FinishEditAsync(claim, Ct));
            Assert.False(await data.Queue(db).FinishEditAsync(claim, Ct));
        }

        Assert.Null(await EditAsync(data, document.Id));
        var job = (await data.JobAsync(document.Id))!;
        Assert.Equal((JobStage.Recognition, 0, (Guid?)null), (job.Stage, job.Attempt, job.ClaimId));
    }

    [Fact]
    public async Task CompleteEdit_StoresNothing_WhenTheDocumentChanged()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "edit_commit_changed");
        var document = await DoneAsync(data);
        var claim = await ClaimedEditAsync(data, document.Id);
        var before = (await EditAsync(data, document.Id))!.Data;
        var completion = new EditCompletion("Neu", [], null, [], """{"x":1}""", Final: false, (tracked, _) =>
        {
            tracked.ReplaceContent("Halb geändert");
            return false;
        });

        await using (var db = data.Context())
        {
            Assert.Equal(EditCommitResult.Changed, await data.Queue(db).CompleteEditAsync(claim, completion, Ct));
            Assert.Equal(FailureOutcome.RetryScheduled, (await data.Queue(db).FailAsync(claim, "Changed", false, Start, Ct)).Outcome);
        }

        Assert.Equal("Rechnung", (await data.DocumentAsync(document.Id)).ContentText);
        Assert.Equal(before, (await EditAsync(data, document.Id))!.Data);
    }

    [Fact]
    public async Task CompleteEdit_MovesTheAppendedDocumentToTheTrash_WithoutItsJob()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "edit_commit_append");
        var target = await DoneAsync(data);
        var appended = await DoneAsync(data, isPrivate: true);
        await using (var db = data.Context())
        {
            db.ProcessingJobs.Add(new ProcessingJob { DocumentId = appended.Id, Stage = JobStage.Recognition, NextAttemptAt = Start.AddDays(1) });
            await db.SaveChangesAsync(Ct);
        }

        var claim = await ClaimedEditAsync(data, target.Id);
        var completion = new EditCompletion(null, null, appended.Id, [TrashItem.ForDocument(appended, Start)], "{}", Final: false, (tracked, other) =>
        {
            tracked.MakePrivate(QueueDatabase.Owner);
            other!.MoveToTrash(QueueDatabase.Owner, Start);
            return true;
        });

        await using (var db = data.Context())
        {
            Assert.Equal(EditCommitResult.Committed, await data.Queue(db).CompleteEditAsync(claim, completion, Ct));
        }

        await using var check = data.Context();
        Assert.True(await check.Documents.IgnoreQueryFilters().AnyAsync(found => found.Id == appended.Id && found.DeletedAt != null, Ct));
        Assert.Null(await data.JobAsync(appended.Id));
        Assert.True((await data.DocumentAsync(target.Id)).IsPrivate);
    }

    [Fact]
    public async Task AFinalCompletion_EndsTheEdit_InTheSameTransaction()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "edit_commit_final");
        var document = await DoneAsync(data);
        var claim = await ClaimedEditAsync(data, document.Id, recognizeAfter: true);
        var completion = new EditCompletion(null, null, null, [TrashItem.ForDocument(document, Start)], "{}", Final: true, (tracked, _) =>
        {
            tracked.MoveToTrash(QueueDatabase.Owner, Start);
            return true;
        });

        await using (var db = data.Context())
        {
            Assert.Equal(EditCommitResult.Committed, await data.Queue(db).CompleteEditAsync(claim, completion, Ct));
        }

        // In the trash nothing is recognized, whatever the edit asked for.
        Assert.Null(await data.JobAsync(document.Id));
        Assert.Null(await EditAsync(data, document.Id));
    }

    [Fact]
    public async Task AGivenUpEdit_DropsThePlan_AndQueuesTheRecognitionItTookOver()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "edit_commit_give_up");
        var document = await DoneAsync(data);
        var claim = await ClaimedEditAsync(data, document.Id, recognizeAfter: true, attemptsSoFar: RetryPolicy.MaxAttempts - 1);

        await using (var db = data.Context())
        {
            var failure = await data.Queue(db).FailAsync(claim, "ExternalToolException: qpdf exited with code 2", false, Start, Ct);
            Assert.Equal(FailureOutcome.GivenUp, failure.Outcome);
        }

        Assert.Null(await EditAsync(data, document.Id));
        Assert.Equal(JobStage.Recognition, (await data.JobAsync(document.Id))!.Stage);
        var stored = await data.DocumentAsync(document.Id);
        Assert.Equal((ProcessingState.Done, "Rechnung"), (stored.State, stored.ContentText));
        Assert.False((await new EfDocumentRepository(data.Context()).GetAsync(document.Id, QueueDatabase.Owner, Ct))!.EditPending);
    }

    [Fact]
    public async Task AGivenUpEdit_WithoutRecognition_LeavesNoJob()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "edit_commit_give_up_plain");
        var document = await DoneAsync(data);
        var claim = await ClaimedEditAsync(data, document.Id);

        await using (var db = data.Context())
        {
            Assert.Equal(FailureOutcome.GivenUp, (await data.Queue(db).FailAsync(claim, "plan", permanent: true, Start, Ct)).Outcome);
        }

        Assert.Null(await data.JobAsync(document.Id));
        Assert.Null(await EditAsync(data, document.Id));
    }
}
