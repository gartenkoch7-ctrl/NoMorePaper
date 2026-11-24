using NoMorePaper.Application.Documents;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Tests.Documents;

/// <summary>An inbox in memory; saving and discarding can be made to fail.</summary>
internal sealed class FakeDocumentStorage : IDocumentStorage
{
    public const string Sha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    public Dictionary<Guid, (string FileName, byte[] Bytes)> Inbox { get; } = [];

    public List<Guid> Discarded { get; } = [];

    public Exception? SaveFailure { get; set; }

    public Exception? DiscardFailure { get; set; }

    /// <summary>What <see cref="OpenRead"/> returns; <c>null</c> means the bytes are gone.</summary>
    public byte[]? StoredBytes { get; set; }

    public async Task<StoredFile> SaveInboxAsync(Guid id, string fileName, Stream content, CancellationToken cancellationToken)
    {
        if (SaveFailure is not null)
        {
            throw SaveFailure;
        }

        using var copy = new MemoryStream();
        await content.CopyToAsync(copy, cancellationToken);
        Inbox[id] = (fileName, copy.ToArray());
        return new StoredFile(copy.Length, Sha256);
    }

    public Task<StoredFile> SaveArchiveAsync(Guid id, DocumentFileKind kind, Stream content, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task ArchiveOriginalAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();

    public void DiscardInbox(Guid id)
    {
        Discarded.Add(id);
        if (DiscardFailure is not null)
        {
            throw DiscardFailure;
        }

        Inbox.Remove(id);
    }

    /// <summary>Ids passed to <see cref="Delete"/>.</summary>
    public List<Guid> Deleted { get; } = [];

    /// <summary>Thrown by <see cref="Delete"/>, after recording the id.</summary>
    public Exception? DeleteFailure { get; set; }

    public void Delete(Guid id)
    {
        Deleted.Add(id);
        if (DeleteFailure is not null)
        {
            throw DeleteFailure;
        }
    }

    public Stream OpenRead(Guid id, DocumentFileKind kind) =>
        StoredBytes is { } bytes ? new MemoryStream(bytes) : throw new FileNotFoundException("Not stored.");

    public Task<StoredFile> SaveEditedAsync(Guid id, Stream content, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>Files <see cref="MoveToTrashAsync"/> moved.</summary>
    public List<(Guid Id, DocumentFileKind Kind, Guid TrashItemId)> Trashed { get; } = [];

    /// <summary>Thrown by <see cref="MoveToTrashAsync"/>, after recording the move.</summary>
    public Exception? TrashFailure { get; set; }

    public Task MoveToTrashAsync(Guid id, DocumentFileKind kind, Guid trashItemId, CancellationToken cancellationToken)
    {
        Trashed.Add((id, kind, trashItemId));
        return TrashFailure is null ? Task.CompletedTask : Task.FromException(TrashFailure);
    }

    public Task<StoredFile> StageAsync(Guid id, Guid editId, DocumentFileKind kind, Stream content, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public bool IsStaged(Guid id, Guid editId, DocumentFileKind kind) => throw new NotSupportedException();

    public Task PublishStagedAsync(Guid id, Guid editId, DocumentFileKind kind, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task TrashStagedAsync(Guid id, Guid editId, Guid trashItemId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task RestoreFromTrashAsync(Guid trashItemId, Guid id, DocumentFileKind kind, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public void PurgeTrash(Guid trashItemId) => throw new NotSupportedException();
}
