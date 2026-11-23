using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Editing;
using NoMorePaper.Infrastructure.Editing;

namespace NoMorePaper.Infrastructure.Processing;

// The page information (document_pages), written in the transaction that stores a new archive.
public sealed partial class JobQueue
{
    // Replaces the pages of the document; joins the caller's open transaction.
    private async Task ReplacePagesAsync(Guid documentId, IReadOnlyList<PageInfo> pages, CancellationToken cancellationToken)
    {
        await db.Set<DocumentPageRow>().Where(row => row.DocumentId == documentId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        db.Set<DocumentPageRow>().AddRange(pages.Select(page => new DocumentPageRow
        {
            DocumentId = documentId,
            Page = page.Page,
            Head = page.Head.Length > PageInfo.MaxHeadLength ? page.Head[..PageInfo.MaxHeadLength] : page.Head,
            ProbablyBlank = page.ProbablyBlank,
            HadTextLayer = page.HadTextLayer,
        }));
    }
}
