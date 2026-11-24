using NoMorePaper.Application.Trash;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Tests.Trash;

/// <summary>
/// Trash entries in memory. The owner of an entry is the owner of its document in <paramref name="documents"/>
/// (shared with <c>FakeDocumentFieldsStore.Documents</c>), as in the database.
/// </summary>
internal sealed class FakeTrashStore(List<Document>? documents = null) : ITrashStore
{
    private readonly List<Document> _documents = documents ?? [];

    public List<TrashItem> Items { get; } = [];

    /// <summary>Documents whose row was locked (<see cref="FindLockedAsync"/>), in order.</summary>
    public List<Guid> Locked { get; } = [];

    /// <summary>Documents deleted for good.</summary>
    public List<Guid> DeletedDocuments { get; } = [];

    public int Saves { get; private set; }

    /// <summary>Thrown by <see cref="SaveAsync"/>, as a failed commit.</summary>
    public Exception? SaveFailure { get; set; }

    public Task AddAsync(TrashItem item, CancellationToken cancellationToken)
    {
        Items.Add(item);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TrashEntry>> ListAsync(Guid ownerId, CancellationToken cancellationToken)
    {
        IReadOnlyList<TrashEntry> entries = [.. Items
            .Where(item => OwnerOf(item) == ownerId)
            .OrderByDescending(item => item.DeletedAt)
            .Select(item => new TrashEntry(
                item,
                item.Kind == TrashItemKind.Edited && DocumentOf(item)?.FindFile(DocumentFileKind.Edited) is not null,
                item.Kind != TrashItemKind.Document && DocumentOf(item)?.IsInTrash == true))];
        return Task.FromResult(entries);
    }

    public Task<TrashItem?> FindAsync(Guid id, Guid ownerId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Find(item => item.Id == id && OwnerOf(item) == ownerId));

    public Task<TrashItem?> FindLockedAsync(Guid id, Guid? ownerId, CancellationToken cancellationToken)
    {
        var item = Items.Find(found => found.Id == id);
        if (item is not null)
        {
            Locked.Add(item.DocumentId);
        }

        return Task.FromResult(item is not null && (ownerId is null || OwnerOf(item) == ownerId) ? item : null);
    }

    public Task<Document?> FindTrashedDocumentAsync(Guid documentId, Guid ownerId, CancellationToken cancellationToken) =>
        Task.FromResult(_documents.Find(document => document.Id == documentId && document.OwnerId == ownerId && document.IsInTrash));

    public Task RemoveAsync(TrashItem item, CancellationToken cancellationToken)
    {
        Items.RemoveAll(found => found.Id == item.Id);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Guid>?> DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var document = _documents.Find(found => found.Id == documentId && found.IsInTrash);
        if (document is null)
        {
            return Task.FromResult<IReadOnlyList<Guid>?>(null);
        }

        IReadOnlyList<Guid> entries = [.. Items.Where(item => item.DocumentId == documentId).Select(item => item.Id)];
        _documents.Remove(document);
        Items.RemoveAll(item => item.DocumentId == documentId);
        DeletedDocuments.Add(documentId);
        return Task.FromResult<IReadOnlyList<Guid>?>(entries);
    }

    public Task SaveAsync(CancellationToken cancellationToken)
    {
        if (SaveFailure is not null)
        {
            throw SaveFailure;
        }

        Saves++;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TrashItem>> DueAsync(DateTimeOffset now, int max, CancellationToken cancellationToken)
    {
        IReadOnlyList<TrashItem> due = [.. Items.Where(item => item.PurgeAfter < now).OrderBy(item => item.PurgeAfter).Take(max)];
        return Task.FromResult(due);
    }

    private Document? DocumentOf(TrashItem item) => _documents.Find(document => document.Id == item.DocumentId);

    private Guid? OwnerOf(TrashItem item) => DocumentOf(item)?.OwnerId;
}
