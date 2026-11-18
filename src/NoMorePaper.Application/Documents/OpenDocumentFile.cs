using NoMorePaper.Application.Accounts;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Documents;

/// <summary>Opens a file of a document the signed-in account may see, exactly as stored (ADR-0003).</summary>
public sealed class OpenDocumentFile(IDocumentRepository documents, IDocumentStorage storage, ICurrentAccount current)
{
    /// <returns>
    /// The file and its bytes, or <c>null</c> if the document, the file or its bytes do not exist, or
    /// the document is private to another account.
    /// </returns>
    public async Task<DocumentFileContent?> OpenAsync(Guid id, DocumentFileKind kind, CancellationToken cancellationToken)
    {
        var detail = await documents.GetAsync(id, current.Id, cancellationToken).ConfigureAwait(false);
        var file = kind == DocumentFileKind.Original ? detail?.Original : detail?.Archive;
        if (file is null)
        {
            return null;
        }

        try
        {
            return new DocumentFileContent(file, storage.OpenRead(id, kind));
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }
}
