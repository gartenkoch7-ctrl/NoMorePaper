using System.Globalization;
using NoMorePaper.Application.Documents;
using NoMorePaper.Application.Processing;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Domain.Editing;

namespace NoMorePaper.Application.Editing;

/// <summary>
/// The files an edit works on, in the attempt's working folder under fixed names (never received ones):
/// the pages are taken from the edited version, else from the archive, which is what the user saw and
/// turned (the archive was straightened and turned by OCR); a replaced page becomes a one-page PDF of its image.
/// </summary>
internal sealed class EditSources(
    IDocumentStorage storage, IWorkingFolder workingFolder, IPdfTool pdf, IPdfComposer composer, IEditPageStorage pageImages)
{
    public const string PdfMediaType = "application/pdf";

    /// <summary>Copies a stored file of the document into the working folder.</summary>
    /// <returns>Its path there.</returns>
    public async Task<string> CopyAsync(Guid id, DocumentFileKind kind, string work, string name, CancellationToken cancellationToken)
    {
        var source = storage.OpenRead(id, kind);
        await using (source.ConfigureAwait(false))
        {
            return await workingFolder.WriteFileAsync(work, name, source, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Copies the file whose pages are edited (edited version, else archive) and counts its pages.</summary>
    /// <param name="archiveOnly">
    /// Takes the archive even when there is an edited version: its pages are the same (OCR made it without
    /// corrections), each with a text layer. A split uses it, so its parts count as having text and skip
    /// the stack separation (<c>SeparationHeuristics.IsScannedStack</c>): they are separated already.
    /// </param>
    /// <exception cref="EditFailedException">The document has neither (never processed).</exception>
    public async Task<(string Path, int Pages)> PagesSourceAsync(
        Document document, string work, string name, bool archiveOnly, CancellationToken cancellationToken)
    {
        var kind = !archiveOnly && document.FindFile(DocumentFileKind.Edited) is not null ? DocumentFileKind.Edited
            : document.FindFile(DocumentFileKind.Archive) is not null ? DocumentFileKind.Archive
            : throw new EditFailedException("The document has no pages to edit", isPermanent: true);
        var path = await CopyAsync(document.Id, kind, work, name, cancellationToken).ConfigureAwait(false);
        return (path, await pdf.CountPagesAsync(path, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>The pages of a plan for <see cref="IPdfTool.ComposeAsync"/>, in order.</summary>
    /// <param name="source">The copied pages source (<see cref="PagesSourceAsync"/>).</param>
    public async Task<IReadOnlyList<PdfPageSource>> PagesAsync(
        Guid id, Guid editId, string source, IReadOnlyList<PageStep> steps, string work, CancellationToken cancellationToken)
    {
        List<PdfPageSource> pages = [];
        foreach (var step in steps)
        {
            if (!step.Replaced)
            {
                pages.Add(new PdfPageSource(source, step.SourcePage, step.Rotation));
                continue;
            }

            using var image = new MemoryStream();
            var jpeg = pageImages.OpenRead(id, editId, step.SourcePage);
            await using (jpeg.ConfigureAwait(false))
            {
                await composer.ComposeImagesAsync([jpeg], image, cancellationToken).ConfigureAwait(false);
            }

            image.Position = 0;
            var name = string.Create(CultureInfo.InvariantCulture, $"replaced-{step.SourcePage}.pdf");
            var path = await workingFolder.WriteFileAsync(work, name, image, cancellationToken).ConfigureAwait(false);
            pages.Add(new PdfPageSource(path, 1, step.Rotation));
        }

        return pages;
    }

    /// <summary>All pages of a copied PDF, unturned.</summary>
    public static IEnumerable<PdfPageSource> AllPages(string path, int count) =>
        Enumerable.Range(1, count).Select(page => new PdfPageSource(path, page, PageRotation.None));

    public Task ComposeAsync(IReadOnlyList<PdfPageSource> pages, string outputPath, CancellationToken cancellationToken) =>
        pdf.ComposeAsync(pages, outputPath, cancellationToken);

    public Stream OpenRead(string path) => workingFolder.OpenRead(path);

    /// <summary>Stores a file of the working folder as the staged edited version or archive of the edit.</summary>
    public async Task<DocumentFile> StageAsync(
        Guid id, Guid editId, DocumentFileKind kind, string path, string fileName, CancellationToken cancellationToken)
    {
        var content = OpenRead(path);
        await using (content.ConfigureAwait(false))
        {
            var stored = await storage.StageAsync(id, editId, kind, content, cancellationToken).ConfigureAwait(false);
            return new DocumentFile(kind, fileName, PdfMediaType, stored.SizeBytes, stored.Sha256);
        }
    }
}
