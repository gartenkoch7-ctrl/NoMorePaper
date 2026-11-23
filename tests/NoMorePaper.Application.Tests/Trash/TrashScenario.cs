using NoMorePaper.Application.Tests.Accounts;
using NoMorePaper.Application.Tests.Classification;
using NoMorePaper.Application.Trash;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Tests.Trash;

/// <summary>The fakes behind the trash use cases, sharing one list of documents, and documents to put in the trash.</summary>
internal sealed class TrashScenario
{
    public const string Sha = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    public static readonly DateTimeOffset Now = new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);

    public TrashScenario()
    {
        Trash = new FakeTrashStore(Documents.Documents);
    }

    public FakeDocumentFieldsStore Documents { get; } = new();

    public FakeTrashStore Trash { get; }

    public TrashFileStorage Storage { get; } = new();

    public RequeueOnlyJobQueue Queue { get; } = new();

    public FakeDocumentEdits Edits { get; } = new();

    public FakeAccountPreferencesStore Preferences { get; } = new();

    public FakeCurrentAccount Current { get; } = new();

    public FixedClock Clock { get; } = new(Now);

    public RecordingLogger<RestoreTrashItem> RestoreLog { get; } = new();

    public RecordingLogger<RestoreTrashFile> FileLog { get; } = new();

    public RecordingLogger<TrashEraser> EraserLog { get; } = new();

    public static DocumentFile File(DocumentFileKind kind, string name = "scan.pdf") => new(kind, name, "application/pdf", 10, Sha);

    /// <summary>A done document of <paramref name="owner"/> (the signed-in account by default) with its original.</summary>
    public Document Done(Guid? owner = null, string text = "Rechnung")
    {
        var document = Documents.Add(owner ?? Current.Id, isPrivate: false);
        document.AttachFile(File(DocumentFileKind.Original));
        document.StartProcessing();
        document.Complete(text);
        return document;
    }

    /// <summary>Moves the document to the trash with its entry, as <see cref="MoveDocumentToTrash"/> does.</summary>
    public TrashItem Trashed(Document document, DateTimeOffset? at = null)
    {
        document.MoveToTrash(document.OwnerId!.Value, at ?? Now.AddDays(-1));
        var item = TrashItem.ForDocument(document, at ?? Now.AddDays(-1));
        Trash.Items.Add(item);
        return item;
    }

    /// <summary>Takes a file away from the document into the trash, as an edit or a choice of versions does.</summary>
    public TrashItem FileTrashed(Document document, DocumentFileKind kind, DateTimeOffset? at = null)
    {
        var file = document.FindFile(kind)!;
        document.RemoveFile(kind);
        var item = TrashItem.ForFile(document, file, at ?? Now.AddDays(-1));
        Trash.Items.Add(item);
        return item;
    }

    public RestoreTrashItem Restore() => new(Trash, RestoreFile(), Queue, Current, RestoreLog);

    public RestoreTrashFile RestoreFile() => new(Trash, Documents, Storage, Queue, Edits, Preferences, Current, Clock, FileLog);

    public TrashEraser Eraser() => new(Trash, Storage, EraserLog);
}
