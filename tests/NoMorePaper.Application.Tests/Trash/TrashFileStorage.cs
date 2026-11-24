using NoMorePaper.Application.Documents;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Tests.Trash;

/// <summary>Records what the trash does to stored files, in order; one kind of step can be made to fail.</summary>
internal sealed class TrashFileStorage : IDocumentStorage
{
    /// <summary>The steps, e.g. <c>to-trash Edited {item}</c>, <c>restore Edited {item}</c>, <c>purge {item}</c>, <c>delete {document}</c>.</summary>
    public List<string> Steps { get; } = [];

    /// <summary>Steps starting with this word throw <see cref="Failure"/> after being recorded.</summary>
    public string? FailingStep { get; set; }

    public Exception Failure { get; set; } = new IOException("disk said no");

    public Task MoveToTrashAsync(Guid id, DocumentFileKind kind, Guid trashItemId, CancellationToken cancellationToken)
    {
        Record($"to-trash {kind} {trashItemId}");
        return Task.CompletedTask;
    }

    public Task RestoreFromTrashAsync(Guid trashItemId, Guid id, DocumentFileKind kind, CancellationToken cancellationToken)
    {
        Record($"restore {kind} {trashItemId}");
        return Task.CompletedTask;
    }

    public void PurgeTrash(Guid trashItemId) => Record($"purge {trashItemId}");

    public void Delete(Guid id) => Record($"delete {id}");

    public Task<StoredFile> SaveInboxAsync(Guid id, string fileName, Stream content, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<StoredFile> SaveArchiveAsync(Guid id, DocumentFileKind kind, Stream content, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task ArchiveOriginalAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();

    public void DiscardInbox(Guid id) => throw new NotSupportedException();

    public Stream OpenRead(Guid id, DocumentFileKind kind) => throw new NotSupportedException();

    public Task<StoredFile> SaveEditedAsync(Guid id, Stream content, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<StoredFile> StageAsync(Guid id, Guid editId, DocumentFileKind kind, Stream content, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public bool IsStaged(Guid id, Guid editId, DocumentFileKind kind) => throw new NotSupportedException();

    public Task PublishStagedAsync(Guid id, Guid editId, DocumentFileKind kind, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task TrashStagedAsync(Guid id, Guid editId, Guid trashItemId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    private void Record(string step)
    {
        Steps.Add(step);
        if (FailingStep is not null && step.StartsWith(FailingStep + " ", StringComparison.Ordinal))
        {
            throw Failure;
        }
    }
}
