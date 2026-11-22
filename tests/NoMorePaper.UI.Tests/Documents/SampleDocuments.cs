using NoMorePaper.Contracts.Classification;
using NoMorePaper.Contracts.Documents;
using NoMorePaper.UI.Tests.Classification;

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

    /// <summary>Two documents with every field of the list: one automatic and private, one confirmed.</summary>
    public static DocumentSummaryDto[] Rich() =>
    [
        new(Guid.NewGuid(), "Stromrechnung", new DateTimeOffset(2026, 11, 18, 10, 0, 0, TimeSpan.Zero), DocumentProcessingState.Done,
            IsPrivate: true, Correspondent: ClassificationSamples.StadtwerkeMuenchen, DocumentType: ClassificationSamples.Rechnung,
            DocumentDate: new DateOnly(2026, 11, 1), Amount: new MoneyDto(84.20m, "EUR"), Recognition: DocumentRecognitionState.Automatic,
            Source: "mail", Excerpt: "Stromrechnung der Stadtwerke für Oktober.", Tags: [ClassificationSamples.Strom]),
        new(Guid.NewGuid(), "Handyvertrag", new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.Zero), DocumentProcessingState.Done,
            Correspondent: ClassificationSamples.Telekom, DocumentType: ClassificationSamples.Vertrag, Recognition: DocumentRecognitionState.Confirmed,
            Source: "upload"),
    ];

    /// <summary>Finished documents titled "Dokument 1" to "Dokument n".</summary>
    public static DocumentSummaryDto[] Many(int count) =>
        [.. Enumerable.Range(1, count).Select(number => new DocumentSummaryDto(
            Guid.NewGuid(), $"Dokument {number}", DateTimeOffset.UtcNow.AddMinutes(-number), DocumentProcessingState.Done))];

    public static DocumentCountsDto Counts(int total, params (EntryDto Type, int Count)[] byType) =>
        new(total, total, [.. byType.Select(item => new DocumentTypeCountDto(item.Type, item.Count))]);

    /// <summary>The smallest file the server accepts as a PDF (it checks the signature, not the extension).</summary>
    // A PDF comment with a fresh id makes every call a different file: the server refuses a second
    // upload of the same bytes as a duplicate (M3).
    public static byte[] MinimalPdf() => System.Text.Encoding.ASCII.GetBytes(
        "%PDF-1.4\n%" + Guid.NewGuid().ToString("N") + "\n"
        + "1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n2 0 obj<</Type/Pages/Count 0/Kids[]>>endobj\n"
        + "trailer<</Root 1 0 R>>\n%%EOF\n");
}
