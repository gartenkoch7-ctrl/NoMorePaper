using NoMorePaper.Application.Accounts;
using NoMorePaper.Application.Classification;
using NoMorePaper.Application.Documents;

namespace NoMorePaper.Application.Editing;

/// <summary>The pages of a document for "Seiten bearbeiten": what is known about each page, and whether the account may edit it now.</summary>
public sealed class GetDocumentPages(
    IDocumentRepository documents, IDocumentFieldsStore fields, IDocumentPages pages, ICurrentAccount current)
{
    /// <returns>The pages, or <c>null</c> if the document does not exist or the account may not see it.</returns>
    public async Task<DocumentPages?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var detail = await documents.GetAsync(id, current.Id, cancellationToken).ConfigureAwait(false);
        var view = detail is null ? null : await fields.GetFieldsAsync(id, current.Id, cancellationToken).ConfigureAwait(false);
        if (detail is null || view is null)
        {
            return null;
        }

        var known = await pages.ListAsync(id, cancellationToken).ConfigureAwait(false);
        return new DocumentPages(id, detail.Title, view.CanEdit, detail.EditPending, known);
    }
}
