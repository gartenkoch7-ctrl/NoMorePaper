using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Application.Accounts;
using NoMorePaper.Application.Classification;
using NoMorePaper.Application.Editing;
using NoMorePaper.Application.Processing;
using NoMorePaper.Application.Recognition;
using NoMorePaper.Domain.Classification;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Classification;
using NoMorePaper.Infrastructure.Editing;
using NoMorePaper.Infrastructure.Inputs;
using NoMorePaper.Infrastructure.Processing;
using NoMorePaper.Infrastructure.Tests.Accounts;
using NoMorePaper.Infrastructure.Tests.Classification;
using NoMorePaper.Infrastructure.Tests.Persistence;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Processing;

/// <summary>Handing a document over during or after recognition: the new owner's view only (ADR-0024).</summary>
[Collection(SharedPostgres.Name)]
public sealed class JobQueueHandOverTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DocumentFields Fields => new("Arztbrief", null, null, null, null, "Befund.", []);

    [Fact]
    public async Task Complete_TakesNothing_WhenTheOwnerChanged_AndHandsTheJobBack()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "queue_owner_changed");
        var id = await data.AddRecognitionJobAsync(QueueDatabase.Start);
        await using var db = data.Context();
        var queue = data.Queue(db);
        var claim = (await queue.ClaimNextAsync(QueueDatabase.LockFor, Ct))!;

        Assert.Equal(RecognitionCommit.OwnerChanged, await queue.CompleteRecognitionAsync(claim, Fields, wasPrivate: false, Guid.CreateVersion7(), Ct));

        var job = await data.JobAsync(id);
        Assert.Equal((0, (Guid?)null), (job!.Attempt, job.ClaimId));
        Assert.Equal(RecognitionState.None, (await data.DocumentAsync(id)).Recognition);
    }

    [Fact]
    public async Task Requeue_StartsRecognitionOver_AndARunningAttemptLosesItsClaim()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "queue_requeue_running");
        var id = await data.AddRecognitionJobAsync(QueueDatabase.Start, priority: 10);
        await using var worker = data.Context();
        var queue = data.Queue(worker);
        var claim = (await queue.ClaimNextAsync(QueueDatabase.LockFor, Ct))!;

        await using var person = data.Context();
        Assert.True(await data.Queue(person).RequeueRecognitionAsync(id, Ct));

        Assert.Equal(RecognitionCommit.ClaimLost, await queue.CompleteRecognitionAsync(claim, Fields, wasPrivate: false, QueueDatabase.Owner, Ct));
        var job = await data.JobAsync(id);
        Assert.Equal((JobStage.Recognition, 0, 0, (Guid?)null), (job!.Stage, job.Priority, job.Attempt, job.ClaimId));
    }

    [Fact]
    public async Task Requeue_QueuesADoneDocumentWithoutJob_AndLeavesOcrAlone()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "queue_requeue_states");
        var done = await data.AddRecognitionJobAsync(QueueDatabase.Start);
        var queued = await data.AddQueuedDocumentAsync(QueueDatabase.Start);
        await using (var db = data.Context())
        {
            await db.ProcessingJobs.Where(job => job.DocumentId == done).ExecuteDeleteAsync(Ct);
        }

        await using var context = data.Context();
        Assert.True(await data.Queue(context).RequeueRecognitionAsync(done, Ct));
        Assert.False(await data.Queue(context).RequeueRecognitionAsync(queued, Ct));

        Assert.Equal((JobStage.Recognition, QueueDatabase.Start), ((await data.JobAsync(done))!.Stage, (await data.JobAsync(done))!.NextAttemptAt));
        Assert.Equal(JobStage.Ocr, (await data.JobAsync(queued))!.Stage);
    }

    [Fact]
    public async Task Requeue_DuringAPlannedEdit_LetsRecognitionFollowTheEdit()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "queue_requeue_edit");
        await using var db = data.Context();
        var document = await Seed.DocumentAsync(db, QueueDatabase.Owner, isPrivate: false, text: "Rechnung");
        var edits = new EfDocumentEdits(db);
        await edits.SaveAsync(new DocumentEdit(document.Id, EditKind.Pages, "{}", QueueDatabase.Owner, QueueDatabase.Start, false), Ct);
        Assert.True(await data.Queue(db).EnqueueEditAsync(document.Id, recognizeAfter: false, Ct));

        Assert.True(await data.Queue(db).RequeueRecognitionAsync(document.Id, Ct));

        Assert.Equal(JobStage.Edit, (await data.JobAsync(document.Id))!.Stage);
        Assert.True((await new EfDocumentEdits(data.Context()).GetAsync(document.Id, Ct))!.RecognizeAfter);
    }

    [Fact]
    public async Task Requeue_DuringAPageAnalysis_TurnsItIntoTheRecognition()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "queue_requeue_analysis");
        await using var db = data.Context();
        var document = await Seed.DocumentAsync(db, QueueDatabase.Owner, isPrivate: false, text: "Rechnung");
        Assert.Equal(1, await data.Queue(db).EnqueuePageBacklogAsync(20, QueueDatabase.Start, Ct));
        var claim = (await data.Queue(db).ClaimNextAsync(QueueDatabase.LockFor, Ct))!;

        await using var person = data.Context();
        Assert.True(await data.Queue(person).RequeueRecognitionAsync(document.Id, Ct));

        Assert.False(await data.Queue(data.Context()).CompleteAnalysisAsync(claim, [], Ct));
        var job = (await data.JobAsync(document.Id))!;
        Assert.Equal((JobStage.Recognition, 0, 0, (Guid?)null), (job.Stage, job.Priority, job.Attempt, job.ClaimId));
    }

    [Fact]
    public async Task HandOver_ChangesOwnerAndQueuesRecognition_InOneTransaction()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "queue_hand_over");
        var admin = await database.AddAdminAsync();
        var bob = await database.AddMemberAsync(admin.Id, "bob");
        var seeded = await Seed.DocumentAsync(database.Db(), admin.Id, isPrivate: false, Seed.Fields(), text: "Arztbrief");
        var db = database.Db();
        var document = await db.Documents.SingleAsync(found => found.Id == seeded.Id, Ct);
        document.MarkOwnerCheck();
        await db.SaveChangesAsync(Ct);

        var scope = database.Db();
        var handOver = new HandOverDocument(
            new EfDocumentFieldsStore(scope),
            database.Store(),
            new EfAccountPreferencesStore(database.Db()),
            new JobQueue(scope, TimeProvider.System),
            new FakeCurrentAccount(admin.Id),
            NullLogger<HandOverDocument>.Instance);

        Assert.Equal(ChangeOutcome.Changed, await handOver.HandOverAsync(seeded.Id, bob.Id, Ct));

        var verify = database.Db();
        var saved = await verify.Documents.AsNoTracking().SingleAsync(found => found.Id == seeded.Id, Ct);
        Assert.Equal((bob.Id, false, false, RecognitionState.None), (saved.OwnerId!.Value, saved.IsPrivate, saved.NeedsOwnerCheck, saved.Recognition));
        var job = await verify.ProcessingJobs.AsNoTracking().SingleAsync(found => found.DocumentId == seeded.Id, Ct);
        Assert.Equal((JobStage.Recognition, 0), (job.Stage, job.Priority));
    }

    [Fact]
    public async Task HandOver_OfConfirmedFields_DropsTheAdminsPrivateEntry_AndKeepsAHouseholdEntry()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "queue_hand_over_confirmed");
        var admin = await database.AddAdminAsync();
        var bob = await database.AddMemberAsync(admin.Id, "bob");
        var doctor = await Seed.EntryAsync(database.Db(), ClassificationKind.Correspondent, "Dr. Privat", admin.Id);
        var tax = await Seed.EntryAsync(database.Db(), ClassificationKind.Tag, "Steuer", admin.Id);
        await Seed.DocumentAsync(database.Db(), admin.Id, isPrivate: false, Seed.Fields(null, null, null, null, tax));
        var seeded = await Seed.DocumentAsync(database.Db(), admin.Id, isPrivate: true, text: "Befund");
        var db = database.Db();
        var document = await db.Documents.SingleAsync(found => found.Id == seeded.Id, Ct);
        document.Edit(admin.Id, new DocumentFields("Befund", doctor, null, null, null, null, [tax]));
        document.MarkOwnerCheck();
        await db.SaveChangesAsync(Ct);

        var scope = database.Db();
        var handOver = new HandOverDocument(
            new EfDocumentFieldsStore(scope),
            database.Store(),
            new EfAccountPreferencesStore(database.Db()),
            new JobQueue(scope, TimeProvider.System),
            new FakeCurrentAccount(admin.Id),
            NullLogger<HandOverDocument>.Instance);

        Assert.Equal(ChangeOutcome.Changed, await handOver.HandOverAsync(seeded.Id, bob.Id, Ct));

        var fields = await new EfDocumentFieldsStore(database.Db()).GetFieldsAsync(seeded.Id, bob.Id, Ct);
        Assert.Null(fields!.Correspondent);
        Assert.Equal(["Steuer"], fields.Tags.Select(tag => tag.Name));
        Assert.Equal(RecognitionState.Confirmed, fields.Recognition);
    }
}
