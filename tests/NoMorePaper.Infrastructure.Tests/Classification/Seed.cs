using NoMorePaper.Domain.Classification;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Persistence;

namespace NoMorePaper.Infrastructure.Tests.Classification;

/// <summary>Writes list entries and documents with fields straight through the model, as recognition would.</summary>
internal static class Seed
{
    private static long s_tick;

    public static async Task<Guid> EntryAsync(AppDbContext db, ClassificationKind kind, string name, Guid createdBy)
    {
        ClassificationEntry entry = kind switch
        {
            ClassificationKind.Correspondent => Correspondent.Create(name, createdBy, DateTimeOffset.UtcNow),
            ClassificationKind.DocumentType => DocumentType.Create(name, createdBy, DateTimeOffset.UtcNow),
            _ => Tag.Create(name, createdBy, DateTimeOffset.UtcNow),
        };
        db.Add(entry);
        await db.SaveChangesAsync();
        return entry.Id;
    }

    /// <summary>A document received a little later than the previous one, so "newest first" is well defined.</summary>
    /// <param name="fields">Recognized fields (<see cref="RecognitionState.Automatic"/>), or none.</param>
    /// <param name="text">Recognized text; the document is <c>Done</c> when given.</param>
    /// <param name="sha256">Hash of the original.</param>
    public static async Task<Document> DocumentAsync(
        AppDbContext db, Guid owner, bool isPrivate, DocumentFields? fields = null, string? text = null, string? sha256 = null)
    {
        var receivedAt = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero).AddSeconds(Interlocked.Increment(ref s_tick));
        var document = Document.Receive("Brief", receivedAt, owner, isPrivate);
        document.AttachFile(new DocumentFile(
            DocumentFileKind.Original, "scan.pdf", "application/pdf", 10, sha256 ?? Convert.ToHexStringLower(Guid.NewGuid().ToByteArray()).PadRight(64, '0')));
        if (fields is not null)
        {
            document.Classify(fields);
        }

        if (text is not null)
        {
            document.StartProcessing();
            document.Complete(text);
        }

        db.Documents.Add(document);
        await db.SaveChangesAsync();
        return document;
    }

    public static DocumentFields Fields(
        Guid? correspondentId = null,
        Guid? documentTypeId = null,
        DateOnly? date = null,
        Money? amount = null,
        params Guid[] tagIds) =>
        new(null, correspondentId, documentTypeId, date, amount, "Kurz.", tagIds);
}
