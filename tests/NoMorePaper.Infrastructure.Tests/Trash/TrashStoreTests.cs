using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Application.Classification;
using NoMorePaper.Application.Editing;
using NoMorePaper.Application.Trash;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Classification;
using NoMorePaper.Infrastructure.Editing;
using NoMorePaper.Infrastructure.Persistence;
using NoMorePaper.Infrastructure.Tests.Accounts;
using NoMorePaper.Infrastructure.Tests.Classification;
using NoMorePaper.Infrastructure.Tests.Persistence;
using NoMorePaper.Infrastructure.Tests.Processing;
using NoMorePaper.Infrastructure.Trash;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Trash;

/// <summary>The trash in Postgres: moving a document there, and the entries of its owner.</summary>
[Collection(SharedPostgres.Name)]
public sealed class TrashStoreTests(PostgresFixture postgres)
{
    private const string Sha = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
    private static readonly DateTimeOffset s_start = QueueDatabase.Start;
    private static readonly Guid s_bob = Guid.CreateVersion7();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task MoveToTrash_HidesTheDocument_DropsJobAndEdit_AndRecordsTheEntry()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "trash_move");
        var id = await data.AddRecognitionJobAsync(s_start);
        await new EfDocumentEdits(data.Context()).SaveAsync(new DocumentEdit(id, EditKind.Pages, "{}", QueueDatabase.Owner, s_start, false), Ct);
        await using var db = data.Context();

        var outcome = await Move(db, data, QueueDatabase.Owner).MoveAsync(id, Ct);

        Assert.Equal(ChangeOutcome.Changed, outcome);
        await using var read = data.Context();
        var document = await read.Documents.IgnoreQueryFilters().AsNoTracking().SingleAsync(d => d.Id == id, Ct);
        Assert.Equal((s_start, (Guid?)QueueDatabase.Owner), (document.DeletedAt, document.DeletedBy));
        Assert.False(await read.Documents.AnyAsync(d => d.Id == id, Ct));
        Assert.Null(await data.JobAsync(id));
        Assert.Null(await new EfDocumentEdits(read).GetAsync(id, Ct));
        var item = Assert.Single(await new EfTrashStore(read).ListAsync(QueueDatabase.Owner, Ct)).Item;
        Assert.Equal((TrashItemKind.Document, id, "Scan 1", s_start.AddDays(30)), (item.Kind, item.DocumentId, item.Title, item.PurgeAfter));
    }

    [Fact]
    public async Task MoveToTrash_ByAnotherAccount_ChangesNothing()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "trash_move_foreign");
        var id = await data.AddRecognitionJobAsync(s_start);
        await using var db = data.Context();

        Assert.Equal(ChangeOutcome.Forbidden, await Move(db, data, s_bob).MoveAsync(id, Ct));

        Assert.NotNull(await data.JobAsync(id));
        Assert.Empty(await new EfTrashStore(data.Context()).ListAsync(QueueDatabase.Owner, Ct));
    }

    [Fact]
    public async Task Entries_BelongToTheDocumentsOwner_AlsoAfterAHandOver()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "trash_owner");
        await using var db = data.Context();
        var document = await Seed.DocumentAsync(db, QueueDatabase.Owner, isPrivate: false, text: "Brief");
        var file = new DocumentFile(DocumentFileKind.Edited, "scan.pdf", "application/pdf", 10, Sha);
        var store = new EfTrashStore(db);
        var older = TrashItem.ForDocument(document, s_start);
        var newer = TrashItem.ForFile(document, file, s_start.AddHours(1));
        await store.AddAsync(older, Ct);
        await store.AddAsync(newer, Ct);

        var listed = await new EfTrashStore(data.Context()).ListAsync(QueueDatabase.Owner, Ct);
        Assert.Equal([newer.Id, older.Id], listed.Select(entry => entry.Item.Id));
        Assert.Equal((DocumentFileKind.Edited, Sha), (listed[0].Item.File?.Kind, listed[0].Item.File?.Sha256));
        Assert.Empty(await store.ListAsync(s_bob, Ct));
        Assert.Null(await store.FindAsync(newer.Id, s_bob, Ct));

        await db.Database.ExecuteSqlAsync($"UPDATE documents SET owner_id = {s_bob} WHERE id = {document.Id}", Ct);

        Assert.Equal(2, (await new EfTrashStore(data.Context()).ListAsync(s_bob, Ct)).Count);
        Assert.Equal(newer.Id, (await new EfTrashStore(data.Context()).FindAsync(newer.Id, s_bob, Ct))?.Id);
        Assert.Empty(await new EfTrashStore(data.Context()).ListAsync(QueueDatabase.Owner, Ct));
    }

    [Fact]
    public async Task FindTrashedDocument_LoadsOnlyTheOwnersDocumentInTheTrash_ForARestore()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "trash_restore");
        await using var db = data.Context();
        var trashed = await Seed.DocumentAsync(db, QueueDatabase.Owner, isPrivate: false, text: "Brief");
        var kept = await Seed.DocumentAsync(db, QueueDatabase.Owner, isPrivate: false, text: "Brief");
        await Trashed.MoveAsync(db, trashed);
        var item = TrashItem.ForDocument(trashed, Trashed.At);
        await new EfTrashStore(db).AddAsync(item, Ct);

        await using var change = data.Context();
        var store = new EfTrashStore(change);
        Assert.Null(await store.FindTrashedDocumentAsync(kept.Id, QueueDatabase.Owner, Ct));
        Assert.Null(await store.FindTrashedDocumentAsync(trashed.Id, s_bob, Ct));
        var document = (await store.FindTrashedDocumentAsync(trashed.Id, QueueDatabase.Owner, Ct))!;
        document.RestoreFromTrash(QueueDatabase.Owner);
        await store.RemoveAsync(item, Ct);
        await store.SaveAsync(Ct);

        await using var read = data.Context();
        Assert.True(await read.Documents.AnyAsync(d => d.Id == trashed.Id, Ct));
        Assert.Empty(await new EfTrashStore(read).ListAsync(QueueDatabase.Owner, Ct));
    }

    [Fact]
    public async Task Due_ListsEntriesWhoseTimeIsUp_OldestFirst_OfEveryAccount()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "trash_due");
        await using var db = data.Context();
        var annas = await Seed.DocumentAsync(db, QueueDatabase.Owner, isPrivate: true, text: "Brief");
        var bobs = await Seed.DocumentAsync(db, s_bob, isPrivate: true, text: "Brief");
        var store = new EfTrashStore(db);
        var oldest = TrashItem.ForDocument(bobs, s_start.AddDays(-40));
        var older = TrashItem.ForDocument(annas, s_start.AddDays(-31));
        await store.AddAsync(older, Ct);
        await store.AddAsync(oldest, Ct);
        await store.AddAsync(TrashItem.ForDocument(annas, s_start.AddDays(-29)), Ct);

        Assert.Equal([oldest.Id, older.Id], (await store.DueAsync(s_start, 10, Ct)).Select(item => item.Id));
        Assert.Equal([oldest.Id], (await store.DueAsync(s_start, 1, Ct)).Select(item => item.Id));
    }

    private static MoveDocumentToTrash Move(AppDbContext db, QueueDatabase data, Guid account) =>
        new(
            new EfDocumentFieldsStore(db),
            new EfTrashStore(db),
            data.Queue(db),
            new EfDocumentEdits(db),
            new FakeCurrentAccount(account),
            data.Time,
            NullLogger<MoveDocumentToTrash>.Instance);
}
