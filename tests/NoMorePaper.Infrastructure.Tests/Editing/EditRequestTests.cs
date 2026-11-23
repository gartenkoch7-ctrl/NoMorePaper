using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Application.Classification;
using NoMorePaper.Application.Editing;
using NoMorePaper.Application.Processing;
using NoMorePaper.Domain.Editing;
using NoMorePaper.Infrastructure.Classification;
using NoMorePaper.Infrastructure.Documents;
using NoMorePaper.Infrastructure.Editing;
using NoMorePaper.Infrastructure.Tests.Accounts;
using NoMorePaper.Infrastructure.Tests.Documents;
using NoMorePaper.Infrastructure.Tests.Persistence;
using NoMorePaper.Infrastructure.Tests.Processing;
using Xunit;
using static NoMorePaper.Infrastructure.Tests.Editing.EditStageData;

namespace NoMorePaper.Infrastructure.Tests.Editing;

/// <summary>
/// Requests on Postgres: an edit takes a waiting separation over, and a refused split leaves the open change
/// as it found it, so a caller that commits it anyway (the split suggestion) changes nothing.
/// </summary>
[Collection(SharedPostgres.Name)]
public sealed class EditRequestTests(PostgresFixture postgres) : IDisposable
{
    private static readonly SplitPlan s_split = new([
        new PagePlan([new PageStep(1, PageRotation.None, false)]),
        new PagePlan([new PageStep(2, PageRotation.None, false)]),
    ]);

    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("nomorepaper-edit-requests-");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _root.Delete(recursive: true);

    [Fact]
    public async Task AnEdit_TakesAWaitingSeparationOver_AndRecognitionFollowsIt()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "edit_request_separation");
        var document = await DoneAsync(data);
        await using (var db = data.Context())
        {
            db.ProcessingJobs.Add(new() { DocumentId = document.Id, Stage = JobStage.Separation, NextAttemptAt = Start.AddMinutes(5), Priority = 10 });
            await db.SaveChangesAsync(Ct);
        }

        await using (var db = data.Context())
        {
            Assert.Equal(ChangeOutcome.Changed, await Split(data, db).RequestAsync(document.Id, s_split, Ct));
        }

        Assert.Equal((JobStage.Edit, 0), ((await data.JobAsync(document.Id))!.Stage, (await data.JobAsync(document.Id))!.Priority));
        Assert.True((await EditAsync(data, document.Id))!.RecognizeAfter);
        await using (var db = data.Context())
        {
            var claim = (await data.Queue(db).ClaimNextAsync(QueueDatabase.LockFor, Ct))!;
            Assert.True(await data.Queue(db).FinishEditAsync(claim, Ct));
        }

        Assert.Equal(JobStage.Recognition, (await data.JobAsync(document.Id))!.Stage);
    }

    [Fact]
    public async Task ARunningSeparation_RefusesTheSplit_AndCommittingTheChangeAfterwards_StoresNothing()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "edit_request_refused");
        var document = await DoneAsync(data);
        await using (var db = data.Context())
        {
            db.ProcessingJobs.Add(new() { DocumentId = document.Id, Stage = JobStage.Separation, NextAttemptAt = Start });
            await db.SaveChangesAsync(Ct);
            Assert.NotNull(await data.Queue(db).ClaimNextAsync(QueueDatabase.LockFor, Ct));
        }

        await using (var db = data.Context())
        {
            var fields = new EfDocumentFieldsStore(db);
            Assert.Equal(ChangeOutcome.Conflict, await Split(data, db, fields).RequestAsync(document.Id, s_split, Ct));
            await fields.SaveAsync(Ct);
        }

        Assert.Null(await EditAsync(data, document.Id));
        Assert.Equal(JobStage.Separation, (await data.JobAsync(document.Id))!.Stage);
    }

    [Fact]
    public async Task APendingEdit_RefusesTheSplit_AndKeepsItsPlan()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "edit_request_pending");
        var document = await DoneAsync(data);
        await ClaimedEditAsync(data, document.Id);
        var plan = (await EditAsync(data, document.Id))!.Data;

        await using (var db = data.Context())
        {
            var fields = new EfDocumentFieldsStore(db);
            Assert.Equal(ChangeOutcome.Conflict, await Split(data, db, fields).RequestAsync(document.Id, s_split, Ct));
            await fields.SaveAsync(Ct);
        }

        Assert.Equal((EditKind.Pages, plan), ((await EditAsync(data, document.Id))!.Kind, (await EditAsync(data, document.Id))!.Data));
    }

    private SplitDocument Split(QueueDatabase data, Infrastructure.Persistence.AppDbContext db, EfDocumentFieldsStore? fields = null) => new(
        new RequestPageEdit(
            fields ?? new EfDocumentFieldsStore(db),
            new EfDocumentPages(db),
            new EfDocumentEdits(db),
            data.Queue(db),
            new EditPageStorage(_root.FullName, new RecordingDirectorySync(), NullLogger<EditPageStorage>.Instance),
            new FakeCurrentAccount(QueueDatabase.Owner),
            data.Time,
            NullLogger<RequestPageEdit>.Instance));
}
