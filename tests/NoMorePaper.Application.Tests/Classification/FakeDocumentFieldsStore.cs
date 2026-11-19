using NoMorePaper.Application.Classification;
using NoMorePaper.Domain.Classification;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Tests.Classification;

/// <summary>Documents and visible entries in memory, with the privacy rule of <see cref="Document.IsVisibleTo"/>.</summary>
internal sealed class FakeDocumentFieldsStore : IDocumentFieldsStore
{
    public List<Document> Documents { get; } = [];

    /// <summary>Entries every account may see; any other id counts as another account's private entry.</summary>
    public HashSet<Guid> VisibleEntries { get; } = [];

    public int Saves { get; private set; }

    public List<Guid> Deleted { get; } = [];

    /// <summary>What <see cref="CountReviewAsync"/> answers.</summary>
    public int ReviewCount { get; set; }

    /// <summary>Viewer of the last read (<see cref="GetFieldsAsync"/>, <see cref="ListReviewAsync"/>, <see cref="CountReviewAsync"/>).</summary>
    public Guid? LastViewer { get; private set; }

    public Task<Document?> FindVisibleAsync(Guid documentId, Guid viewerId, CancellationToken cancellationToken) =>
        Task.FromResult(Documents.Find(document => document.Id == documentId && document.IsVisibleTo(viewerId)));

    public Task<bool> AreVisibleAsync(Guid viewerId, ClassificationKind kind, IReadOnlyCollection<Guid> entryIds, CancellationToken cancellationToken) =>
        Task.FromResult(entryIds.All(VisibleEntries.Contains));

    public Task SaveAsync(CancellationToken cancellationToken)
    {
        Saves++;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Document document, CancellationToken cancellationToken)
    {
        Documents.Remove(document);
        Deleted.Add(document.Id);
        return Task.CompletedTask;
    }

    public Task<DocumentFieldsView?> GetFieldsAsync(Guid documentId, Guid viewerId, CancellationToken cancellationToken)
    {
        LastViewer = viewerId;
        return Task.FromResult<DocumentFieldsView?>(null);
    }

    public Task<IReadOnlyList<ReviewItem>> ListReviewAsync(Guid viewerId, CancellationToken cancellationToken)
    {
        LastViewer = viewerId;
        return Task.FromResult<IReadOnlyList<ReviewItem>>([]);
    }

    public Task<int> CountReviewAsync(Guid viewerId, CancellationToken cancellationToken)
    {
        LastViewer = viewerId;
        return Task.FromResult(ReviewCount);
    }

    /// <summary>Adds a document of <paramref name="owner"/> to the store.</summary>
    public Document Add(Guid owner, bool isPrivate)
    {
        var document = Document.Receive("Brief", DateTimeOffset.UnixEpoch, owner, isPrivate);
        Documents.Add(document);
        return document;
    }
}
