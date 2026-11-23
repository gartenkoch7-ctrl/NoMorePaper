using NoMorePaper.Application.Editing;
using NoMorePaper.Application.Processing;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Editing;
using NoMorePaper.Infrastructure.Processing;
using NoMorePaper.Infrastructure.Tests.Processing;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Editing;

/// <summary>Processed documents with original and archive, and planned edits with their job, as a request leaves them.</summary>
internal static class EditStageData
{
    public static readonly DateTimeOffset Start = QueueDatabase.Start;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static string Sha(char c) => new(c, 64);

    public static DocumentFile File(DocumentFileKind kind, char sha) => new(kind, kind + ".pdf", "application/pdf", 10, Sha(sha));

    /// <summary>A done document of <see cref="QueueDatabase.Owner"/> with original and archive.</summary>
    public static async Task<Document> DoneAsync(QueueDatabase data, string text = "Rechnung", bool isPrivate = false)
    {
        await using var db = data.Context();
        var document = Document.Receive("Brief", Start, QueueDatabase.Owner, isPrivate);
        document.AttachFile(File(DocumentFileKind.Original, 'a'));
        document.StartProcessing();
        document.Complete(text);
        document.AttachFile(File(DocumentFileKind.Archive, 'b'));
        db.Documents.Add(document);
        await db.SaveChangesAsync(Ct);
        return document;
    }

    /// <summary>Plans an edit of the document and queues its job, then claims it.</summary>
    public static async Task<JobClaim> ClaimedEditAsync(QueueDatabase data, Guid id, bool recognizeAfter = false, int attemptsSoFar = 0)
    {
        await using var db = data.Context();
        await new EfDocumentEdits(db).SaveAsync(
            new DocumentEdit(id, EditKind.Pages, """{"editId":"00000000-0000-7000-8000-000000000000"}""", QueueDatabase.Owner, Start, recognizeAfter), Ct);
        db.ProcessingJobs.Add(new ProcessingJob { DocumentId = id, Stage = JobStage.Edit, NextAttemptAt = Start, Attempt = attemptsSoFar });
        await db.SaveChangesAsync(Ct);
        return (await data.Queue(db).ClaimNextAsync(QueueDatabase.LockFor, Ct))!;
    }

    public static async Task<DocumentEdit?> EditAsync(QueueDatabase data, Guid id)
    {
        await using var db = data.Context();
        return await new EfDocumentEdits(db).GetAsync(id, Ct);
    }
}
