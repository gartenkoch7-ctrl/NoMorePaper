namespace NoMorePaper.Application.Documents;

/// <summary>Makes PDFs for the inputs: camera pages into one document, a mail text into a page (ADR-0024).</summary>
public interface IPdfComposer
{
    /// <summary>One page per image, in the given order.</summary>
    /// <param name="jpegs">JPEG images, at least one; read from their current position.</param>
    /// <param name="target">Receives the PDF; left open.</param>
    Task ComposeImagesAsync(IReadOnlyList<Stream> jpegs, Stream target, CancellationToken cancellationToken);

    /// <summary>A text document on A4 pages: heading, header fields, then the body with line and page breaks.</summary>
    /// <param name="target">Receives the PDF; left open.</param>
    Task ComposeTextAsync(TextPage page, Stream target, CancellationToken cancellationToken);
}
