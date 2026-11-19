using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Documents;
using NoMorePaper.Application.Processing;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Persistence;

namespace NoMorePaper.Infrastructure.Processing;

/// <summary>
/// The processing queue in Postgres (<c>processing_jobs</c>). Workers claim the due job with the lowest
/// priority number, the earliest among equals, with <c>FOR UPDATE SKIP LOCKED</c>, so several Workers
/// never get the same job, and hold it until <c>locked_until</c>; a crashed Worker's job becomes due
/// again once that has passed. Failures are in <c>JobQueue.Failure.cs</c>, the recognition stage in
/// <c>JobQueue.Recognition.cs</c>.
/// </summary>
/// <remarks>
/// Every claim gets a new <see cref="JobClaim.ClaimId"/>, and every later call acts only while the
/// job still carries it. A Worker whose lock expired and whose job another Worker has claimed since
/// therefore changes nothing, even when that claim was handed back and the job claimed once more.
/// <c>attempt</c> only counts attempts and is no token: handing a job back takes its attempt back.
/// </remarks>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "A queue in the domain sense (docs/plan/m1-durchstich.md), not a collection type.")]
public sealed partial class JobQueue(AppDbContext db, TimeProvider time) : IJobQueue
{
    private const string ArchiveMediaType = "application/pdf";
    private const string ArchiveExtension = ".pdf";
    private const string FallbackArchiveFileName = "archive.pdf";

    /// <inheritdoc />
    public async Task<JobClaim?> ClaimNextAsync(TimeSpan lockFor, bool includeRecognition, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lockFor, TimeSpan.Zero);
        var now = time.GetUtcNow();
        var lockedUntil = now + lockFor;
        var claimId = Guid.NewGuid();
        var ocr = nameof(JobStage.Ocr);

        // One statement: select, lock and update cannot interleave with another Worker's claim.
        var claimed = await db.ProcessingJobs
            .FromSql(
                $"""
                UPDATE processing_jobs SET attempt = attempt + 1, claim_id = {claimId}, locked_until = {lockedUntil}
                WHERE document_id = (
                    SELECT document_id FROM processing_jobs
                    WHERE next_attempt_at <= {now} AND (locked_until IS NULL OR locked_until < {now})
                      AND (stage = {ocr} OR {includeRecognition})
                    ORDER BY priority, next_attempt_at
                    LIMIT 1
                    FOR UPDATE SKIP LOCKED)
                RETURNING document_id, stage, priority, attempt, claim_id, next_attempt_at, locked_until, last_error
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return claimed.SingleOrDefault() is { } job ? new JobClaim(job.DocumentId, claimId, job.Attempt, job.Stage) : null;
    }

    /// <inheritdoc />
    public async Task<Document?> StartAsync(JobClaim claim, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claim);
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            if (await LockClaimedJobAsync(claim, cancellationToken).ConfigureAwait(false) is null)
            {
                return null;
            }

            var document = await FindDocumentAsync(claim.DocumentId, cancellationToken).ConfigureAwait(false);
            document.StartProcessing();
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return document;
        }
    }

    /// <inheritdoc />
    public async Task<bool> CompleteAsync(JobClaim claim, string text, StoredFile archive, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(archive);

        var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var job = await LockClaimedJobAsync(claim, cancellationToken).ConfigureAwait(false);
            if (job is null)
            {
                return false;
            }

            var document = await FindDocumentAsync(claim.DocumentId, cancellationToken).ConfigureAwait(false);
            document.AttachFile(new DocumentFile(
                DocumentFileKind.Archive, ArchiveFileName(document), ArchiveMediaType, archive.SizeBytes, archive.Sha256));
            document.Complete(text);

            // Recognition follows at once, with attempts of its own and the job's priority, unless there
            // is nothing to recognize: no text, no owner whose lists it could use (M1, before setup), or
            // fields somebody confirmed already while OCR ran.
            if (document.OwnerId is null || string.IsNullOrWhiteSpace(text) || document.Recognition == RecognitionState.Confirmed)
            {
                db.ProcessingJobs.Remove(job);
            }
            else
            {
                job.Stage = JobStage.Recognition;
                job.Attempt = 0;
                job.NextAttemptAt = time.GetUtcNow();
                job.ClaimId = null;
                job.LockedUntil = null;
                job.LastError = null;
            }

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
    }

    // Row lock until the transaction ends: a concurrent claim skips the job instead of taking it over.
    private async Task<ProcessingJob?> LockClaimedJobAsync(JobClaim claim, CancellationToken cancellationToken)
    {
        var jobs = await db.ProcessingJobs
            .FromSql(
                $"""
                SELECT document_id, stage, priority, attempt, claim_id, next_attempt_at, locked_until, last_error FROM processing_jobs
                WHERE document_id = {claim.DocumentId} AND claim_id = {claim.ClaimId}
                FOR UPDATE
                """)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return jobs.SingleOrDefault();
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
