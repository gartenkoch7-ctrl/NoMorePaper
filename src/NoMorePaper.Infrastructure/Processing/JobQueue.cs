using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Documents;
using NoMorePaper.Application.Processing;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Domain.Processing;
using NoMorePaper.Infrastructure.Persistence;

namespace NoMorePaper.Infrastructure.Processing;

/// <summary>
/// The processing queue in Postgres (<c>processing_jobs</c>). Workers claim the earliest due job
/// with <c>FOR UPDATE SKIP LOCKED</c>, so several Workers never get the same job, and hold it
/// until <c>locked_until</c>; a crashed Worker's job becomes due again once that has passed.
/// </summary>
/// <remarks>
/// Every claim gets a new <see cref="JobClaim.ClaimId"/>, and every later call acts only while the
/// job still carries it. A Worker whose lock expired and whose job another Worker has claimed since
/// therefore changes nothing, even when that claim was handed back and the job claimed once more.
/// <c>attempt</c> only counts attempts and is no token: handing a job back takes its attempt back.
/// </remarks>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "A queue in the domain sense (docs/plan/m1-durchstich.md), not a collection type.")]
public sealed class JobQueue(AppDbContext db, TimeProvider time) : IJobQueue
{
    private const string ArchiveMediaType = "application/pdf";
    private const string ArchiveExtension = ".pdf";
    private const string FallbackArchiveFileName = "archive.pdf";

    /// <summary>Claims the earliest due job that nobody holds, counting it as a new attempt.</summary>
    /// <param name="lockFor">How long the job stays claimed; longer than processing may take.</param>
    /// <returns>The claim, or <c>null</c> when no job is due.</returns>
    public async Task<JobClaim?> ClaimNextAsync(TimeSpan lockFor, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lockFor, TimeSpan.Zero);
        var now = time.GetUtcNow();
        var lockedUntil = now + lockFor;
        var claimId = Guid.NewGuid();

        // One statement: select, lock and update cannot interleave with another Worker's claim.
        var claimed = await db.ProcessingJobs
            .FromSql(
                $"""
                UPDATE processing_jobs SET attempt = attempt + 1, claim_id = {claimId}, locked_until = {lockedUntil}
                WHERE document_id = (
                    SELECT document_id FROM processing_jobs
                    WHERE next_attempt_at <= {now} AND (locked_until IS NULL OR locked_until < {now})
                    ORDER BY next_attempt_at
                    LIMIT 1
                    FOR UPDATE SKIP LOCKED)
                RETURNING document_id, attempt, claim_id, next_attempt_at, locked_until, last_error
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return claimed.SingleOrDefault() is { } job ? new JobClaim(job.DocumentId, claimId, job.Attempt) : null;
    }

    /// <summary>Marks the document as being processed and returns it with its files.</summary>
    /// <returns>The document, or <c>null</c> when the claim is lost and nothing was changed.</returns>
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

    /// <summary>Stores the result of a successful attempt and removes the job, in one transaction.</summary>
    /// <param name="text">Recognized text; empty when the document has none.</param>
    /// <param name="archive">What the storage wrote for the archive PDF.</param>
    /// <returns><c>false</c> when the claim is lost and nothing was changed.</returns>
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
            db.ProcessingJobs.Remove(job);

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
    }

    /// <summary>
    /// Records a failed attempt: schedules the next one after the <see cref="RetryPolicy"/> delay,
    /// or, when all attempts are used up or the failure is permanent, marks the document as failed
    /// and removes the job.
    /// </summary>
    /// <param name="technicalError">Technical reason without document content; cut to <see cref="Document.MaxFailureReasonLength"/>.</param>
    /// <param name="permanent">Another attempt would fail the same way, so the document fails at once.</param>
    /// <param name="now">Time of the failure.</param>
    /// <returns>What was recorded; <see cref="FailureOutcome.ClaimLost"/> when nothing was changed.</returns>
    public async Task<RecordedFailure> FailAsync(
        JobClaim claim, string technicalError, bool permanent, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentException.ThrowIfNullOrWhiteSpace(technicalError);

        // Changes of the failed attempt (e.g. a half-done completion) must not be saved with the failure.
        db.ChangeTracker.Clear();
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var job = await LockClaimedJobAsync(claim, cancellationToken).ConfigureAwait(false);
            if (job is null)
            {
                return new RecordedFailure(FailureOutcome.ClaimLost);
            }

            var error = technicalError.Length > Document.MaxFailureReasonLength
                ? technicalError[..Document.MaxFailureReasonLength]
                : technicalError;
            var next = permanent ? null : RetryPolicy.NextAttemptAt(Math.Max(job.Attempt, 1), now.ToUniversalTime());
            if (next is { } retryAt)
            {
                job.LastError = error;
                job.NextAttemptAt = retryAt;
                job.ClaimId = null;
                job.LockedUntil = null;
            }
            else
            {
                await GiveUpAsync(job, error, cancellationToken).ConfigureAwait(false);
            }

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return next is null ? new RecordedFailure(FailureOutcome.GivenUp) : new RecordedFailure(FailureOutcome.RetryScheduled, next);
        }
    }

    /// <summary>
    /// Hands an interrupted attempt back (e.g. on shutdown): the job is due again at once and the
    /// attempt does not count.
    /// </summary>
    /// <returns><c>false</c> when the claim is lost and nothing was changed.</returns>
    public async Task<bool> ReleaseAsync(JobClaim claim, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claim);

        // Taking the attempt back happens at most once per claim: the claim id goes with it.
        var released = await db.ProcessingJobs
            .Where(job => job.DocumentId == claim.DocumentId && job.ClaimId == claim.ClaimId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(job => job.Attempt, job => job.Attempt - 1)
                    .SetProperty(job => job.ClaimId, (Guid?)null)
                    .SetProperty(job => job.LockedUntil, (DateTimeOffset?)null),
                cancellationToken)
            .ConfigureAwait(false);
        return released > 0;
    }

    // Row lock until the transaction ends: a concurrent claim skips the job instead of taking it over.
    private async Task<ProcessingJob?> LockClaimedJobAsync(JobClaim claim, CancellationToken cancellationToken)
    {
        var jobs = await db.ProcessingJobs
            .FromSql(
                $"""
                SELECT document_id, attempt, claim_id, next_attempt_at, locked_until, last_error FROM processing_jobs
                WHERE document_id = {claim.DocumentId} AND claim_id = {claim.ClaimId}
                FOR UPDATE
                """)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return jobs.SingleOrDefault();
    }

    private async Task GiveUpAsync(ProcessingJob job, string error, CancellationToken cancellationToken)
    {
        var document = await FindDocumentAsync(job.DocumentId, cancellationToken).ConfigureAwait(false);

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
