using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Documents;

/// <summary>
/// Documents in the database: recording received ones, the read side of list and detail, and who may
/// see them. Visibility is filtered in the query, never after loading.
/// </summary>
public interface IDocumentRepository
{
    /// <summary>
    /// Records a received document with its files and its processing job, due at
    /// <see cref="Document.ReceivedAt"/>, in one transaction.
    /// </summary>
    Task AddReceivedAsync(Document document, CancellationToken cancellationToken);

    /// <summary>Whether a document with this id is recorded.</summary>
    /// <exception cref="System.Data.Common.DbException">The database could not answer.</exception>
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// The documents <paramref name="viewerId"/> may see (own ones and all that are not private) that
    /// match <paramref name="filter"/>, newest first.
    /// </summary>
    /// <param name="page">Page number, starting at 1.</param>
    /// <param name="pageSize">Documents per page, at least 1.</param>
    Task<IReadOnlyList<DocumentSummary>> ListAsync(
        Guid viewerId, int page, int pageSize, DocumentFilter filter, CancellationToken cancellationToken);

    /// <summary>
    /// A document <paramref name="viewerId"/> may see whose original has exactly these bytes. Documents
    /// the viewer may not see are never found, so a refused upload cannot reveal them.
    /// </summary>
    /// <param name="sha256">SHA-256 of the original, 64 lowercase hex characters.</param>
    /// <returns>The oldest such document, or <c>null</c>.</returns>
    Task<Guid?> FindVisibleBySha256Async(Guid viewerId, string sha256, CancellationToken cancellationToken);

    /// <summary>
    /// A document <paramref name="ownerId"/> owns whose original has exactly these bytes. Shared documents
    /// of other accounts do not count (background inputs, <see cref="DuplicateScope.Owned"/>).
    /// </summary>
    /// <param name="sha256">SHA-256 of the original, 64 lowercase hex characters.</param>
    /// <returns>The oldest such document, or <c>null</c>.</returns>
    Task<Guid?> FindOwnedBySha256Async(Guid ownerId, string sha256, CancellationToken cancellationToken);

    /// <returns>
    /// The document, or <c>null</c> if it does not exist, has no original file, or is private to
    /// another account: callers cannot tell these apart, so a private document's existence stays hidden.
    /// </returns>
    Task<DocumentDetail?> GetAsync(Guid id, Guid viewerId, CancellationToken cancellationToken);

    /// <summary>Makes a document private or shares it again, if <paramref name="ownerId"/> owns it.</summary>
    /// <returns><c>false</c> if the document does not exist or belongs to another account.</returns>
    Task<bool> SetPrivacyAsync(Guid id, Guid ownerId, bool isPrivate, CancellationToken cancellationToken);
}
