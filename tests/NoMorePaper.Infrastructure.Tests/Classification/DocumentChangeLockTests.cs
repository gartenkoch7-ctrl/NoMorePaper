using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Processing;
using NoMorePaper.Application.Recognition;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Classification;
using NoMorePaper.Infrastructure.Tests.Persistence;
using NoMorePaper.Infrastructure.Tests.Processing;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Classification;

/// <summary>A person changing a document and the Worker finishing its recognition take turns on the row.</summary>
[Collection(SharedPostgres.Name)]
public sealed class DocumentChangeLockTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DocumentFields Fields => new(
        "Stromrechnung November 2026", null, null, new DateOnly(2026, 11, 1), new Money(84.2m, "EUR"), "Abschlag für November.", []);

    [Fact]
    public async Task Recognition_WaitsForAConfirmation_AndThenLeavesTheConfirmedFields()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "change_lock_confirm");
        var id = await data.AddRecognitionJobAsync(QueueDatabase.Start);
        await using var worker = data.Context();
        var queue = data.Queue(worker);
        var claim = (await queue.ClaimNextAsync(QueueDatabase.LockFor, Ct))!;

        await using var person = data.Context();
        var store = new EfDocumentFieldsStore(person);
        var document = (await store.FindVisibleAsync(id, QueueDatabase.Owner, Ct))!;
        document.Confirm(QueueDatabase.Owner);

        var completing = queue.CompleteRecognitionAsync(claim, Fields, wasPrivate: false, QueueDatabase.Owner, Ct);
        await Task.Delay(TimeSpan.FromMilliseconds(500), Ct);
        Assert.False(completing.IsCompleted);

        await store.SaveAsync(Ct);

        Assert.Equal(RecognitionCommit.JobRemoved, await completing);
        var saved = await data.DocumentAsync(id);
        Assert.Equal((RecognitionState.Confirmed, "Scan 1"), (saved.Recognition, saved.Title));
        Assert.Null(await data.JobAsync(id));
    }

    [Fact]
    public async Task ARefusedChange_FreesTheLock_WhenTheContextIsDisposed()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "change_lock_refused");
        var id = await data.AddRecognitionJobAsync(QueueDatabase.Start);
        await using var worker = data.Context();
        var queue = data.Queue(worker);
        var claim = (await queue.ClaimNextAsync(QueueDatabase.LockFor, Ct))!;

        var person = data.Context();
        Assert.NotNull(await new EfDocumentFieldsStore(person).FindVisibleAsync(id, QueueDatabase.Owner, Ct));
        var completing = queue.CompleteRecognitionAsync(claim, Fields, wasPrivate: false, QueueDatabase.Owner, Ct);
        await person.DisposeAsync();

        Assert.Equal(RecognitionCommit.Classified, await completing);
        Assert.Equal(RecognitionState.Automatic, (await data.DocumentAsync(id)).Recognition);
    }

    [Fact]
    public async Task Recognition_OfAPrivateDocument_SharedJustBeforeTheCommit_TakesNothing_AndHandsTheJobBack()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "change_lock_shared");
        var id = await data.AddRecognitionJobAsync(QueueDatabase.Start, isPrivate: true);
        await using var worker = data.Context();
        var queue = data.Queue(worker);
        var claim = (await queue.ClaimNextAsync(QueueDatabase.LockFor, Ct))!;

        await using (var owner = data.Context())
        {
            var document = await owner.Documents.SingleAsync(found => found.Id == id, Ct);
            document.Share(QueueDatabase.Owner);
            await owner.SaveChangesAsync(Ct);
        }

        Assert.Equal(RecognitionCommit.SharedMeanwhile, await queue.CompleteRecognitionAsync(claim, Fields, wasPrivate: true, QueueDatabase.Owner, Ct));

        var saved = await data.DocumentAsync(id);
        Assert.Equal((RecognitionState.None, "Scan 1", false), (saved.Recognition, saved.Title, saved.IsPrivate));
        var job = await data.JobAsync(id);
        Assert.NotNull(job);
        Assert.Equal((0, (Guid?)null, (DateTimeOffset?)null), (job.Attempt, job.ClaimId, job.LockedUntil));
        await using var next = data.Context();
        Assert.Equal(id, (await data.Queue(next).ClaimNextAsync(QueueDatabase.LockFor, Ct))?.DocumentId);
    }
}
