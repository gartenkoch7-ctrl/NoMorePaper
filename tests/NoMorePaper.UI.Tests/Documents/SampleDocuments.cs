using NoMorePaper.Contracts.Documents;

namespace NoMorePaper.UI.Tests.Documents;

/// <summary>Sample API data for the page tests.</summary>
internal static class SampleDocuments
{
    public static DocumentSummaryDto[] OneOfEachState()
    {
        var now = DateTimeOffset.UtcNow;
        return
        [
            new(Guid.NewGuid(), "Stromrechnung", now, DocumentProcessingState.Queued),
            new(Guid.NewGuid(), "Mietvertrag", now.AddMinutes(-1), DocumentProcessingState.Processing),
            new(Guid.NewGuid(), "Steuerbescheid", now.AddMinutes(-2), DocumentProcessingState.Done),
            new(Guid.NewGuid(), "Kaputter Scan", now.AddMinutes(-3), DocumentProcessingState.Failed),
        ];
    }

    /// <summary>The smallest file the server accepts as a PDF (it checks the signature, not the extension).</summary>
    // A PDF comment with a fresh id makes every call a different file: the server refuses a second
    // upload of the same bytes as a duplicate (M3).
    public static byte[] MinimalPdf() => System.Text.Encoding.ASCII.GetBytes(
        "%PDF-1.4\n%" + Guid.NewGuid().ToString("N") + "\n"
        + "1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n2 0 obj<</Type/Pages/Count 0/Kids[]>>endobj\n"
        + "trailer<</Root 1 0 R>>\n%%EOF\n");
}
