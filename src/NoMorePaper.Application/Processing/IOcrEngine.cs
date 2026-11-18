namespace NoMorePaper.Application.Processing;

/// <summary>
/// Turns a received original into the searchable archive PDF (PDF/A) and returns its text
/// (ADR-0004). Runs locally; no document leaves the host.
/// </summary>
public interface IOcrEngine
{
    /// <summary>Recognizes the text of <paramref name="inputPath"/> and writes the archive PDF.</summary>
    /// <param name="inputPath">The original, copied to a working folder of the caller.</param>
    /// <param name="mediaType">Media type of the original, detected from its content: PDF, JPEG, PNG or HEIC.</param>
    /// <param name="outputPdfPath">Where the PDF/A is written; its folder is the caller's working folder.</param>
    /// <exception cref="NotSupportedException">The media type cannot be processed.</exception>
    /// <exception cref="ExternalToolException">A processing step failed; <see cref="ExternalToolException.IsPermanent"/> when retrying cannot help.</exception>
    /// <exception cref="TimeoutException">Processing took longer than the time limit per document.</exception>
    Task<OcrResult> CreateArchiveAsync(string inputPath, string mediaType, string outputPdfPath, CancellationToken cancellationToken);
}
