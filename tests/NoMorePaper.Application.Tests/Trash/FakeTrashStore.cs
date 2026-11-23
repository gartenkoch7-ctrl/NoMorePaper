using NoMorePaper.Application.Trash;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Tests.Trash;

/// <summary>Trash entries in memory; reads are not needed by the use cases tested so far.</summary>
internal sealed class FakeTrashStore : ITrashStore
{
    public List<TrashItem> Items { get; } = [];

    public Task AddAsync(TrashItem item, CancellationToken cancellationToken)
    {
        Items.Add(item);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TrashItem>> ListAsync(Guid ownerId, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<TrashItem?> FindAsync(Guid id, Guid ownerId, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<Document?> FindTrashedDocumentAsync(Guid documentId, Guid ownerId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task RemoveAsync(TrashItem item, CancellationToken cancellationToken)
    {
        Items.Remove(item);
        return Task.CompletedTask;
    }

    public Task SaveAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<IReadOnlyList<TrashItem>> DueAsync(DateTimeOffset now, int max, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
