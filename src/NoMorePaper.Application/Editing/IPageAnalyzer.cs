namespace NoMorePaper.Application.Editing;

/// <summary>
/// Reads each page of an archive PDF after OCR: the start of its text, whether it is probably blank,
/// and whether the original page had text already. Runs in the Worker only.
/// </summary>
public interface IPageAnalyzer
{
    /// <param name="pdfPath">The archive PDF in the working folder.</param>
    /// <param name="originalPath">The working copy of the source it was made from; may be an image.</param>
    /// <returns>
    /// One entry per page of <paramref name="pdfPath"/>, in order; empty when the pages could not be read.
    /// Only cancellation is thrown: the pages are an extra and never fail the OCR attempt.
    /// </returns>
    Task<IReadOnlyList<PageInfo>> AnalyzeAsync(string pdfPath, string originalPath, CancellationToken cancellationToken);
}
