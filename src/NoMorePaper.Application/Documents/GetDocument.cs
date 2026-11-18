namespace NoMorePaper.Application.Documents;

/// <summary>One document with its files, for the detail view.</summary>
public sealed class GetDocument(IDocumentRepository documents)
{
    /// <returns>The document, or <c>null</c> if it does not exist.</returns>
    public Task<DocumentDetail?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        documents.GetAsync(id, cancellationToken);
}
