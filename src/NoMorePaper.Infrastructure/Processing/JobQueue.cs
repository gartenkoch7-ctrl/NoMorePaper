using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Documents;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Domain.Processing;
using NoMorePaper.Infrastructure.Persistence;

namespace NoMorePaper.Infrastructure.Processing;

/// <summary>
/// The processing queue in Postgres (<c>processing_jobs</c>). Workers claim the earliest due job
/// with <c>FOR UPDATE SKIP LOCKED</c>, so several Workers never get the same job, and hold it
/// until <c>locked_until</c>; a crashed Worker's job becomes due again once that has passed.
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "A queue in the domain sense (docs/plan/m1-durchstich.md), not a collection type.")]
public sealed class JobQueue(AppDbContext db, TimeProvider time)
{
    private const string ArchiveMediaType = "application/pdf";
    private const string ArchiveExtension = ".pdf";
    private const string FallbackArchiveFileName = "archive.pdf";

    /// <summary>Claims the earliest due job that nobody holds, counting it as a new attempt.</summary>
    /// <param name="lockFor">How long the job stays claimed; longer than processing may take.</param>
    /// <returns>The claimed job with its new attempt number, or <c>null</c> when no job is due.</returns>
    public async Task<ProcessingJob?> ClaimNextAsync(TimeSpan lockFor, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lockFor, TimeSpan.Zero);
        var now = time.GetUtcNow();
        var lockedUntil = now + lockFor;

        // One statement: select, lock and update cannot interleave with another Worker's claim.
        var claimed = await db.ProcessingJobs
            .FromSql(
                $"""
                UPDATE processing_jobs SET attempt = attempt + 1, locked_until = {lockedUntil}
                WHERE document_id = (
                    SELECT document_id FROM processing_jobs
                    WHERE next_attempt_at <= {now} AND (locked_until IS NULL OR locked_until < {now})
                    ORDER BY next_attempt_at
                    LIMIT 1
                    FOR UPDATE SKIP LOCKED)
                RETURNING document_id, attempt, next_attempt_at, locked_until, last_error
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return claimed.SingleOrDefault();
    }

    /// <summary>Marks the document as being processed and returns it with its files.</summary>
    public async Task<Document> StartAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var document = await FindDocumentAsync(documentId, cancellationToken).ConfigureAwait(false);
        document.StartProcessing();
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return document;
    }

    /// <summary>Stores the result of a successful attempt and removes the job, in one transaction.</summary>
    /// <param name="text">Recognized text; empty when the document has none.</param>
    /// <param name="archive">What the storage wrote for the archive PDF.</param>
    public async Task CompleteAsync(Guid documentId, string text, StoredFile archive, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(archive);

        var document = await FindDocumentAsync(documentId, cancellationToken).ConfigureAwait(false);
        document.AttachFile(new DocumentFile(
            DocumentFileKind.Archive, ArchiveFileName(document), ArchiveMediaType, archive.SizeBytes, archive.Sha256));
        document.Complete(text);

        var job = await db.ProcessingJobs.SingleOrDefaultAsync(j => j.DocumentId == documentId, cancellationToken).ConfigureAwait(false);
        if (job is not null)
        {
            db.ProcessingJobs.Remove(job);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Records a failed attempt: schedules the next one after the <see cref="RetryPolicy"/> delay,
    /// or, when all attempts are used up, marks the document as failed and removes the job.
    /// </summary>
    /// <param name="technicalError">Technical reason without document content; cut to <see cref="Document.MaxFailureReasonLength"/>.</param>
    /// <param name="now">Time of the failure.</param>
    /// <returns>When the job runs again, or <c>null</c> when it was given up (or no longer exists).</returns>
    public async Task<DateTimeOffset?> FailAsync(Guid documentId, string technicalError, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(technicalError);

        // Changes of the failed attempt (e.g. a half-done completion) must not be saved with the failure.
        db.ChangeTracker.Clear();
        var job = await db.ProcessingJobs.SingleOrDefaultAsync(j => j.DocumentId == documentId, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            return null;
        }

        var error = technicalError.Length > Document.MaxFailureReasonLength
            ? technicalError[..Document.MaxFailureReasonLength]
            : technicalError;
        var next = RetryPolicy.NextAttemptAt(Math.Max(job.Attempt, 1), now.ToUniversalTime());
        if (next is { } retryAt)
        {
            job.LastError = error;
            job.NextAttemptAt = retryAt;
            job.LockedUntil = null;
        }
        else
        {
            var document = await FindDocumentAsync(documentId, cancellationToken).ConfigureAwait(false);

            // Queued: the attempt failed before it could even mark the document as processing.
            if (document.State == ProcessingState.Queued)
            {
                document.StartProcessing();
            }

            if (document.State == ProcessingState.Processing)
            {
                document.Fail(error);
            }

            db.ProcessingJobs.Remove(job);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return next;
    }

    private Task<Document> FindDocumentAsync(Guid documentId, CancellationToken cancellationToken) =>
        db.Documents.SingleAsync(document => document.Id == documentId, cancellationToken);

    // Offered on download: the received name with the archive's extension, e.g. "Scan 1.pdf".
    private static string ArchiveFileName(Document document)
    {
        var baseName = Path.GetFileNameWithoutExtension(document.FindFile(DocumentFileKind.Original)?.FileName);
        if (string.IsNullOrWhiteSpace(baseName))
        {
            return FallbackArchiveFileName;
        }

        var maxBaseLength = DocumentFile.MaxFileNameLength - ArchiveExtension.Length;
        return (baseName.Length > maxBaseLength ? baseName[..maxBaseLength] : baseName) + ArchiveExtension;
    }
}
