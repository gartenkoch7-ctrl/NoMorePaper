using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Documents;

/// <summary>Documents in the database: recording received ones and the read side of list and detail.</summary>
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

    /// <summary>Newest first.</summary>
    /// <param name="page">Page number, starting at 1.</param>
    /// <param name="pageSize">Documents per page, at least 1.</param>
    Task<IReadOnlyList<DocumentSummary>> ListAsync(int page, int pageSize, CancellationToken cancellationToken);

    /// <returns>The document, or <c>null</c> if it does not exist or has no original file.</returns>
    Task<DocumentDetail?> GetAsync(Guid id, CancellationToken cancellationToken);
}
