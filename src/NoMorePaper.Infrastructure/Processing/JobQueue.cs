using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Application.Documents;
using NoMorePaper.Application.Editing;
using NoMorePaper.Application.Processing;
using NoMorePaper.Application.Separation;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Persistence;

namespace NoMorePaper.Infrastructure.Processing;

/// <summary>
/// The processing queue in Postgres (<c>processing_jobs</c>). Workers claim the due job with the lowest
/// priority number, the earliest among equals, with <c>FOR UPDATE SKIP LOCKED</c>, so several Workers
/// never get the same job, and hold it until <c>locked_until</c>; a crashed Worker's job becomes due
/// again once that has passed. Failures are in <c>JobQueue.Failure.cs</c>, the recognition stage in
/// <c>JobQueue.Recognition.cs</c>, queuing edits and dropping jobs in <c>JobQueue.Enqueue.cs</c>, the page
/// information in <c>JobQueue.Pages.cs</c>, the stack separation in <c>JobQueue.Separation.cs</c>.
/// </summary>
/// <remarks>
/// Every claim gets a new <see cref="JobClaim.ClaimId"/>, and every later call acts only while the
/// job still carries it. A Worker whose lock expired and whose job another Worker has claimed since
/// therefore changes nothing, even when that claim was handed back and the job claimed once more.
/// <c>attempt</c> only counts attempts and is no token: handing a job back takes its attempt back.
/// </remarks>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "A queue in the domain sense (docs/plan/m1-durchstich.md), not a collection type.")]
public sealed partial class JobQueue(AppDbContext db, TimeProvider time, ILogger<JobQueue>? logger = null) : IJobQueue
{
    private readonly ILogger _logger = logger ?? NullLogger<JobQueue>.Instance;

    private const string ArchiveMediaType = "application/pdf";
    private const string ArchiveExtension = ".pdf";
    private const string FallbackArchiveFileName = "archive.pdf";

    /// <inheritdoc />
    public async Task<JobClaim?> ClaimNextAsync(TimeSpan lockFor, bool includeLlmStages, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lockFor, TimeSpan.Zero);
        var now = time.GetUtcNow();
        var lockedUntil = now + lockFor;
        var claimId = Guid.NewGuid();
        var ocr = nameof(JobStage.Ocr);
        var edit = nameof(JobStage.Edit);

        // One statement: select, lock and update cannot interleave with another Worker's claim. A job of
        // a document in the trash is never claimed (moving it there removes the job anyway).
        // {0} claim, {1} locked until, {2} now, {3} OCR, {4} edit, {5} LLM stages too.
        var claimed = await db.ProcessingJobs
            .FromSql(FormattableStringFactory.Create(
                $$"""
                UPDATE processing_jobs SET attempt = attempt + 1, claim_id = {0}, locked_until = {1}
                WHERE document_id = (
                    SELECT document_id FROM processing_jobs
                    WHERE next_attempt_at <= {2} AND (locked_until IS NULL OR locked_until < {2})
                      AND (stage IN ({3}, {4}) OR {5})
                      AND EXISTS (SELECT 1 FROM documents d WHERE d.id = processing_jobs.document_id AND {{TrashSql.NotInTrash}})
                    ORDER BY priority, next_attempt_at
                    LIMIT 1
                    FOR UPDATE SKIP LOCKED)
                RETURNING document_id, stage, priority, attempt, claim_id, next_attempt_at, locked_until, last_error
                """,
                claimId,
                lockedUntil,
                now,
                ocr,
                edit,
                includeLlmStages))
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
            await LockDocumentAsync(claim.DocumentId, cancellationToken).ConfigureAwait(false);
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
    public async Task<bool> CompleteAsync(
        JobClaim claim, string text, StoredFile archive, IReadOnlyList<PageInfo> pages, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentNullException.ThrowIfNull(pages);

        var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            await LockDocumentAsync(claim.DocumentId, cancellationToken).ConfigureAwait(false);
            var job = await LockClaimedJobAsync(claim, cancellationToken).ConfigureAwait(false);
            if (job is null)
            {
                return false;
            }

            var document = await FindDocumentAsync(claim.DocumentId, cancellationToken).ConfigureAwait(false);
            document.AttachFile(new DocumentFile(
                DocumentFileKind.Archive, ArchiveFileName(document), ArchiveMediaType, archive.SizeBytes, archive.Sha256));
            document.Complete(text);
            await ReplacePagesAsync(claim.DocumentId, pages, cancellationToken).ConfigureAwait(false);

            // A scanned stack may hold several documents: separation looks first, recognition follows it.
            MoveOn(job, document, SeparationHeuristics.IsScannedStack(pages) ? JobStage.Separation : JobStage.Recognition);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
    }

    // The next stage follows at once, with attempts of its own and the job's priority, unless there is
    // nothing to recognize: no text, no owner whose lists it could use (M1, before setup), or fields
    // somebody confirmed already while OCR ran. Separation only prepares recognition, so the same holds.
    private void MoveOn(ProcessingJob job, Document document, JobStage next)
    {
        if (document.OwnerId is null || string.IsNullOrWhiteSpace(document.ContentText) || document.Recognition == RecognitionState.Confirmed)
        {
            db.ProcessingJobs.Remove(job);
            return;
        }

        job.Stage = next;
        job.Attempt = 0;
        job.NextAttemptAt = time.GetUtcNow();
        job.ClaimId = null;
        job.LockedUntil = null;
        job.LastError = null;
    }

    // The document first, then the job: moving a document to the trash and deleting it lock them in this
    // order too (the job is removed in the same transaction or by cascade), so the two never wait for
    // each other. Once the lock is granted a trashed document's job is gone and the claim reads as lost.
    private async Task LockDocumentAsync(Guid documentId, CancellationToken cancellationToken) =>
        await db.Database.ExecuteSqlAsync($"SELECT 1 FROM documents WHERE id = {documentId} FOR NO KEY UPDATE", cancellationToken)
            .ConfigureAwait(false);

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
