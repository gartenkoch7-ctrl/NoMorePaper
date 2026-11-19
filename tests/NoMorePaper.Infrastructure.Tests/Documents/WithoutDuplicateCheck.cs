using NoMorePaper.Application.Documents;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Infrastructure.Tests.Documents;

/// <summary>
/// The real repository, except that no upload is a duplicate: for tests of the commit that need the
/// duplicate check to pass on a database that cannot answer it.
/// </summary>
internal sealed class WithoutDuplicateCheck(IDocumentRepository inner) : IDocumentRepository
{
    public Task AddReceivedAsync(Document document, CancellationToken cancellationToken) => inner.AddReceivedAsync(document, cancellationToken);

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) => inner.ExistsAsync(id, cancellationToken);

    public Task<IReadOnlyList<DocumentSummary>> ListAsync(
        Guid viewerId, int page, int pageSize, DocumentFilter filter, CancellationToken cancellationToken) =>
        inner.ListAsync(viewerId, page, pageSize, filter, cancellationToken);

    public Task<Guid?> FindVisibleBySha256Async(Guid viewerId, string sha256, CancellationToken cancellationToken) =>
        Task.FromResult<Guid?>(null);

    public Task<DocumentDetail?> GetAsync(Guid id, Guid viewerId, CancellationToken cancellationToken) =>
        inner.GetAsync(id, viewerId, cancellationToken);

    public Task<bool> SetPrivacyAsync(Guid id, Guid ownerId, bool isPrivate, CancellationToken cancellationToken) =>
        inner.SetPrivacyAsync(id, ownerId, isPrivate, cancellationToken);
}
