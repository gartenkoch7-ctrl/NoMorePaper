using NoMorePaper.Application.Accounts;

namespace NoMorePaper.Application.Classification;

/// <summary>The fields of a document as the signed-in account sees them.</summary>
public sealed class GetDocumentFields(IDocumentFieldsStore documents, ICurrentAccount current)
{
    /// <returns>The fields, or <c>null</c> if the document does not exist or is private to another account.</returns>
    public Task<DocumentFieldsView?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        documents.GetFieldsAsync(id, current.Id, cancellationToken);
}
