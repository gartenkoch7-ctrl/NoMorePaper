using NoMorePaper.Application.Documents;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Infrastructure.Tests.Inputs.ScannerFolder;

/// <summary>The real repository, except that recording a received document fails, like a database that went away.</summary>
internal sealed class FailingCommit(IDocumentRepository inner) : IDocumentRepository
{
    public Task AddReceivedAsync(Document document, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("The database is away.");

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) => inner.ExistsAsync(id, cancellationToken);

    public Task<IReadOnlyList<DocumentSummary>> ListAsync(
        Guid viewerId, int page, int pageSize, DocumentFilter filter, CancellationToken cancellationToken) =>
        inner.ListAsync(viewerId, page, pageSize, filter, cancellationToken);

    public Task<Guid?> FindVisibleBySha256Async(Guid viewerId, string sha256, CancellationToken cancellationToken) =>
        inner.FindVisibleBySha256Async(viewerId, sha256, cancellationToken);

    public Task<Guid?> FindOwnedBySha256Async(Guid ownerId, string sha256, CancellationToken cancellationToken) =>
        inner.FindOwnedBySha256Async(ownerId, sha256, cancellationToken);

    public Task<DocumentDetail?> GetAsync(Guid id, Guid viewerId, CancellationToken cancellationToken) =>
        inner.GetAsync(id, viewerId, cancellationToken);

    public Task<bool> SetPrivacyAsync(Guid id, Guid ownerId, bool isPrivate, CancellationToken cancellationToken) =>
        inner.SetPrivacyAsync(id, ownerId, isPrivate, cancellationToken);
}
