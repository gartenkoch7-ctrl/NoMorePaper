namespace NoMorePaper.Application.Editing;

/// <summary>
/// Lossless page operations on PDFs (qpdf, ADR-0025): choose, turn, reorder and join pages. Runs in
/// the Worker only, on files in its working folder.
/// </summary>
public interface IPdfTool
{
    /// <summary>Writes a PDF of the given pages, in the given order.</summary>
    /// <exception cref="Processing.ExternalToolException">The tool failed.</exception>
    /// <exception cref="TimeoutException">The tool ran too long.</exception>
    Task ComposeAsync(IReadOnlyList<PdfPageSource> pages, string outputPath, CancellationToken cancellationToken);

    /// <summary>How many pages the PDF has.</summary>
    /// <exception cref="Processing.ExternalToolException">The tool failed, e.g. the file is no PDF.</exception>
    Task<int> CountPagesAsync(string path, CancellationToken cancellationToken);
}
