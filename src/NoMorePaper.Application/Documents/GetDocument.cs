using NoMorePaper.Application.Accounts;

namespace NoMorePaper.Application.Documents;

/// <summary>One document with its files, for the detail view of the signed-in account.</summary>
public sealed class GetDocument(IDocumentRepository documents, ICurrentAccount current)
{
    /// <returns>The document, or <c>null</c> if it does not exist or is private to another account.</returns>
    public Task<DocumentDetail?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        documents.GetAsync(id, current.Id, cancellationToken);
}
