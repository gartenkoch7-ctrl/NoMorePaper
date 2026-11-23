using NoMorePaper.Application.Accounts;

namespace NoMorePaper.Application.Documents;

/// <summary>The recognised text of one document, for the viewer of the signed-in account.</summary>
public sealed class GetDocumentText(IDocumentTextReader reader, ICurrentAccount current)
{
    /// <returns>The text, or <c>null</c> if the document does not exist or is private to another account.</returns>
    public Task<DocumentText?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        reader.GetAsync(id, current.Id, cancellationToken);
}
