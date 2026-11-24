using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Processing;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Editing;

/// <summary>
/// The page backlog (M4b, F1): reads the pages of a document processed before page information existed,
/// from its archive, without OCR (<see cref="IPageAnalyzer"/>). Whether a page had text before it was
/// scanned is read from the original when there is one.
/// </summary>
internal sealed class PageAnalysis(EditSources sources, IPageAnalyzer analyzer, ILogger<EditStageRunner> logger)
{
    /// <returns><c>false</c> when the claim was lost and nothing was stored.</returns>
    public async Task<bool> RunAsync(IJobQueue queue, JobClaim claim, string work, CancellationToken cancellationToken)
    {
        var document = await queue.ReadAsync(claim, cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return false;
        }

        var id = document.Id;
        IReadOnlyList<PageInfo> pages = [];
        if (document.FindFile(DocumentFileKind.Archive) is not null)
        {
            var archive = await sources.CopyAsync(id, DocumentFileKind.Archive, work, "archive.pdf", cancellationToken).ConfigureAwait(false);
            var original = document.FindFile(DocumentFileKind.Original) is not null
                ? await sources.CopyAsync(id, DocumentFileKind.Original, work, "original", cancellationToken).ConfigureAwait(false)
                : archive;
            pages = await analyzer.AnalyzeAsync(archive, original, cancellationToken).ConfigureAwait(false);
        }

        if (!await queue.CompleteAnalysisAsync(claim, pages, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        logger.PagesAnalyzed(id, pages.Count);
        return true;
    }
}
