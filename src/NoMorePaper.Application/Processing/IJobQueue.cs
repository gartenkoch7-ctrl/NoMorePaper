using System.Diagnostics.CodeAnalysis;
using NoMorePaper.Application.Documents;
using NoMorePaper.Application.Recognition;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Domain.Processing;

namespace NoMorePaper.Application.Processing;

/// <summary>
/// The processing queue (docs/kb/verarbeitung.md). Workers claim the due job with the lowest priority
/// number, the earliest among equals, so several Workers never get the same one, and hold it until the
/// lock expires; a crashed Worker's job becomes due again after that. A job goes through the
/// <see cref="JobStage"/>s in order: OCR, then recognition.
/// </summary>
/// <remarks>
/// Every claim gets a new <see cref="JobClaim.ClaimId"/>, and every later call acts only while the
/// job still carries it. A Worker whose lock expired and whose job another Worker has claimed since
/// therefore changes nothing, even when that claim was handed back and the job claimed once more.
/// The attempt number only counts attempts and is no token: handing a job back takes its attempt back.
/// </remarks>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "A queue in the domain sense (docs/plan/m1-durchstich.md), not a collection type.")]
public interface IJobQueue
{
    /// <summary>
    /// Claims the due job nobody holds with the lowest priority number, the earliest among equals,
    /// counting it as a new attempt of its stage.
    /// </summary>
    /// <param name="lockFor">How long the job stays claimed; longer than processing may take.</param>
    /// <param name="includeRecognition">Whether jobs at <see cref="JobStage.Recognition"/> may be claimed (model ready).</param>
    /// <returns>The claim, or <c>null</c> when no job is due.</returns>
    Task<JobClaim?> ClaimNextAsync(TimeSpan lockFor, bool includeRecognition, CancellationToken cancellationToken);

    /// <summary>Marks the document as being processed and returns it with its files (OCR stage).</summary>
    /// <returns>The document, or <c>null</c> when the claim is lost and nothing was changed.</returns>
    Task<Document?> StartAsync(JobClaim claim, CancellationToken cancellationToken);

    /// <summary>
    /// Stores the result of a successful OCR attempt and moves the job on to
    /// <see cref="JobStage.Recognition"/>, due at once with its attempts reset, in one transaction. A
    /// document without text, without owner or with confirmed fields has nothing to recognize: its job
    /// is removed instead.
    /// </summary>
    /// <param name="text">Recognized text; empty when the document has none.</param>
    /// <param name="archive">What the storage wrote for the archive PDF.</param>
    /// <returns><c>false</c> when the claim is lost and nothing was changed.</returns>
    Task<bool> CompleteAsync(JobClaim claim, string text, StoredFile archive, CancellationToken cancellationToken);

    /// <summary>
    /// Records a failed attempt: schedules the next one after the <see cref="RetryPolicy"/> delay,
    /// or, when all attempts are used up or the failure is permanent, removes the job. A job given up
    /// at <see cref="JobStage.Ocr"/> marks the document as failed; at <see cref="JobStage.Recognition"/>
    /// the document stays done without fields.
    /// </summary>
    /// <param name="technicalError">Technical reason without document content; cut to <see cref="Document.MaxFailureReasonLength"/>.</param>
    /// <param name="permanent">Another attempt would fail the same way, so the job is given up at once.</param>
    /// <param name="now">Time of the failure.</param>
    /// <returns>What was recorded; <see cref="FailureOutcome.ClaimLost"/> when nothing was changed.</returns>
    Task<RecordedFailure> FailAsync(
        JobClaim claim, string technicalError, bool permanent, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>
    /// Hands an interrupted attempt back (e.g. on shutdown): the job is due again at once and the
    /// attempt does not count.
    /// </summary>
    /// <returns><c>false</c> when the claim is lost and nothing was changed.</returns>
    Task<bool> ReleaseAsync(JobClaim claim, CancellationToken cancellationToken);

    /// <summary>The claimed job's document as it is, without changing it (recognition stage).</summary>
    /// <returns>The document, or <c>null</c> when the claim is lost.</returns>
    Task<Document?> ReadAsync(JobClaim claim, CancellationToken cancellationToken);

    /// <summary>
    /// Ends the recognition stage in one transaction: takes <paramref name="fields"/> with
    /// <see cref="Document.Classify"/>, unless the document has been confirmed by now, and removes the job.
    /// </summary>
    /// <param name="fields">Recognized fields, or <c>null</c> to only remove the job.</param>
    Task<RecognitionCommit> CompleteRecognitionAsync(JobClaim claim, DocumentFields? fields, CancellationToken cancellationToken);

    /// <summary>
    /// Queues recognition for every done document with text and owner that has no fields
    /// (<see cref="RecognitionState.None"/>) and no job, e.g. documents from before recognition existed.
    /// </summary>
    /// <param name="priority">Priority of the new jobs; a higher number runs after new uploads.</param>
    /// <param name="now">When the new jobs are due.</param>
    /// <returns>How many jobs were queued.</returns>
    Task<int> EnqueueRecognitionBacklogAsync(int priority, DateTimeOffset now, CancellationToken cancellationToken);
}
