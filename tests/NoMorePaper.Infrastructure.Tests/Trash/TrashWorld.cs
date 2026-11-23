using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Application.Trash;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Classification;
using NoMorePaper.Infrastructure.Documents;
using NoMorePaper.Infrastructure.Editing;
using NoMorePaper.Infrastructure.Inputs;
using NoMorePaper.Infrastructure.Persistence;
using NoMorePaper.Infrastructure.Tests.Accounts;
using NoMorePaper.Infrastructure.Tests.Documents;
using NoMorePaper.Infrastructure.Tests.Persistence;
using NoMorePaper.Infrastructure.Tests.Processing;
using NoMorePaper.Infrastructure.Trash;

namespace NoMorePaper.Infrastructure.Tests.Trash;

/// <summary>
/// The trash use cases wired like the Server, on a migrated database of their own and a data root on disk:
/// each use case gets a fresh context, as a request scope would.
/// </summary>
internal sealed class TrashWorld : IDisposable
{
    private readonly DirectoryInfo _sandbox = Directory.CreateTempSubdirectory("nomorepaper-trash-cases-");

    private TrashWorld(QueueDatabase data)
    {
        Data = data;
        Root = Path.Combine(_sandbox.FullName, "data");
        Storage = new FileSystemDocumentStorage(Root, new RecordingDirectorySync(), NullLogger<FileSystemDocumentStorage>.Instance);
    }

    public QueueDatabase Data { get; }

    public string Root { get; }

    public FileSystemDocumentStorage Storage { get; }

    public static async Task<TrashWorld> CreateAsync(PostgresFixture postgres, string database) =>
        new(await QueueDatabase.CreateAsync(postgres, database));

    public string TrashFolder(Guid trashItemId) => Path.Combine(Root, "trash", trashItemId.ToString());

    // One trash store per scope, as in DI: the store that opened the change commits it.
    public RestoreTrashItem Restore(AppDbContext db, Guid account)
    {
        var trash = new EfTrashStore(db);
        return new(trash, RestoreFile(db, trash, account), Data.Queue(db), new FakeCurrentAccount(account), NullLogger<RestoreTrashItem>.Instance);
    }

    public PurgeTrashItem Purge(AppDbContext db, Guid account) =>
        new(Eraser(db, new EfTrashStore(db)), new FakeCurrentAccount(account), NullLogger<PurgeTrashItem>.Instance);

    public PurgeDueTrash DuePurge(AppDbContext db)
    {
        var trash = new EfTrashStore(db);
        return new(trash, Eraser(db, trash), Data.Time, NullLogger<PurgeDueTrash>.Instance);
    }

    public MoveDocumentToTrash Move(AppDbContext db, Guid account) =>
        new(
            new EfDocumentFieldsStore(db),
            new EfTrashStore(db),
            Data.Queue(db),
            new EfDocumentEdits(db),
            new FakeCurrentAccount(account),
            Data.Time,
            NullLogger<MoveDocumentToTrash>.Instance);

    /// <summary>Stores <paramref name="content"/> as the edited version of the document and records it.</summary>
    public async Task<DocumentFile> EditAsync(AppDbContext db, Document document, string content)
    {
        var stored = await Storage.SaveEditedAsync(document.Id, new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)), default);
        var file = new DocumentFile(DocumentFileKind.Edited, "edited.pdf", "application/pdf", stored.SizeBytes, stored.Sha256);
        document.ReplaceFile(file, Data.Time.GetUtcNow());
        await db.SaveChangesAsync();
        return file;
    }

    /// <summary>Moves a file of the document to the trash, as an edit or a choice of versions does.</summary>
    public async Task<TrashItem> FileToTrashAsync(AppDbContext db, Document document, DocumentFileKind kind)
    {
        var item = TrashItem.ForFile(document, document.FindFile(kind)!, Data.Time.GetUtcNow());
        document.RemoveFile(kind);
        await new EfTrashStore(db).AddAsync(item, default);
        await Storage.MoveToTrashAsync(document.Id, kind, item.Id, default);
        return item;
    }

    public string ReadEdited(Guid documentId)
    {
        using var reader = new StreamReader(Storage.OpenRead(documentId, DocumentFileKind.Edited));
        return reader.ReadToEnd();
    }

    public void Dispose() => _sandbox.Delete(recursive: true);

    private RestoreTrashFile RestoreFile(AppDbContext db, EfTrashStore trash, Guid account) =>
        new(
            trash,
            new EfDocumentFieldsStore(db),
            Storage,
            Data.Queue(db),
            new EfDocumentEdits(db),
            new EfAccountPreferencesStore(db),
            new FakeCurrentAccount(account),
            Data.Time,
            NullLogger<RestoreTrashFile>.Instance);

    private TrashEraser Eraser(AppDbContext db, EfTrashStore trash) => new(trash, Storage, new EfDocumentEdits(db), NullLogger<TrashEraser>.Instance);
}
