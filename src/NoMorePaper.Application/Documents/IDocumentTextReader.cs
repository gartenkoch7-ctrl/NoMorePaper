namespace NoMorePaper.Application.Documents;

/// <summary>Reads the recognised text of one document, applying the visibility rule of the document list.</summary>
public interface IDocumentTextReader
{
    /// <returns>The text, or <c>null</c> if the document does not exist or is private to another account.</returns>
    Task<DocumentText?> GetAsync(Guid id, Guid viewerId, CancellationToken cancellationToken);
}
