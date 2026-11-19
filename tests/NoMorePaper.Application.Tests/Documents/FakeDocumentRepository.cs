using NoMorePaper.Application.Documents;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Tests.Documents;

/// <summary>Documents in memory; each call can be made to fail. Records which account asked.</summary>
internal sealed class FakeDocumentRepository : IDocumentRepository
{
    public List<Document> Added { get; } = [];

    /// <summary>Thrown by <see cref="AddReceivedAsync"/>; when <see cref="RecordDespiteFailure"/>, after recording.</summary>
    public Exception? AddFailure { get; set; }

    /// <summary>The commit reached the database although the caller hears of a failure.</summary>
    public bool RecordDespiteFailure { get; set; }

    public Exception? ExistsFailure { get; set; }

    public CancellationToken? AddToken { get; private set; }

    /// <summary>Viewer, page and page size of the last <see cref="ListAsync"/> call.</summary>
    public (Guid Viewer, int Page, int PageSize)? LastList { get; private set; }

    /// <summary>The filter of the last <see cref="ListAsync"/> call.</summary>
    public DocumentFilter? LastFilter { get; private set; }

    /// <summary>What <see cref="FindVisibleBySha256Async"/> answers: an earlier document with the same file.</summary>
    public Guid? VisibleDuplicate { get; set; }

    /// <summary>Viewer and hash of the last <see cref="FindVisibleBySha256Async"/> call.</summary>
    public (Guid Viewer, string Sha256)? LastHashLookup { get; private set; }

    /// <summary>The account of the last <see cref="GetAsync"/> call.</summary>
    public Guid? LastViewer { get; private set; }

    /// <summary>Id, owner and wish of the last <see cref="SetPrivacyAsync"/> call.</summary>
    public (Guid Id, Guid Owner, bool IsPrivate)? LastPrivacy { get; private set; }

    public bool PrivacyResult { get; set; } = true;

    public DocumentDetail? Detail { get; set; }

    public Task AddReceivedAsync(Document document, CancellationToken cancellationToken)
    {
        AddToken = cancellationToken;
        if (AddFailure is not null && !RecordDespiteFailure)
        {
            throw AddFailure;
        }

        Added.Add(document);
        return AddFailure is null ? Task.CompletedTask : throw AddFailure;
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) =>
        ExistsFailure is not null ? throw ExistsFailure : Task.FromResult(Added.Exists(document => document.Id == id));

    public Task<IReadOnlyList<DocumentSummary>> ListAsync(
        Guid viewerId, int page, int pageSize, DocumentFilter filter, CancellationToken cancellationToken)
    {
        LastList = (viewerId, page, pageSize);
        LastFilter = filter;
        return Task.FromResult<IReadOnlyList<DocumentSummary>>([]);
    }

    public Task<Guid?> FindVisibleBySha256Async(Guid viewerId, string sha256, CancellationToken cancellationToken)
    {
        LastHashLookup = (viewerId, sha256);
        return Task.FromResult(VisibleDuplicate);
    }

    /// <summary>What <see cref="FindOwnedBySha256Async"/> answers: an earlier document of the owner with the same file.</summary>
    public Guid? OwnedDuplicate { get; set; }

    /// <summary>Owner and hash of the last <see cref="FindOwnedBySha256Async"/> call.</summary>
    public (Guid Owner, string Sha256)? LastOwnedLookup { get; private set; }

    public Task<Guid?> FindOwnedBySha256Async(Guid ownerId, string sha256, CancellationToken cancellationToken)
    {
        LastOwnedLookup = (ownerId, sha256);
        return Task.FromResult(OwnedDuplicate);
    }

    public Task<DocumentDetail?> GetAsync(Guid id, Guid viewerId, CancellationToken cancellationToken)
    {
        LastViewer = viewerId;
        return Task.FromResult(Detail?.Id == id ? Detail : null);
    }

    public Task<bool> SetPrivacyAsync(Guid id, Guid ownerId, bool isPrivate, CancellationToken cancellationToken)
    {
        LastPrivacy = (id, ownerId, isPrivate);
        return Task.FromResult(PrivacyResult);
    }
}
