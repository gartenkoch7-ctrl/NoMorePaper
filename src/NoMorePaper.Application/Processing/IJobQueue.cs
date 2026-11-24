using System.Diagnostics.CodeAnalysis;
using NoMorePaper.Application.Documents;
using NoMorePaper.Application.Editing;
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
    /// counting it as a new attempt of its stage. Jobs of documents in the trash are never claimed.
    /// </summary>
    /// <param name="lockFor">How long the job stays claimed; longer than processing may take.</param>
    /// <param name="includeLlmStages">
    /// Whether jobs at the stages that ask the local LLM (<see cref="JobStage.Recognition"/>,
    /// <see cref="JobStage.Separation"/>) may be claimed (model ready); OCR and edits always may.
    /// </param>
    /// <returns>The claim, or <c>null</c> when no job is due.</returns>
    Task<JobClaim?> ClaimNextAsync(TimeSpan lockFor, bool includeLlmStages, CancellationToken cancellationToken);

    /// <summary>Marks the document as being processed and returns it with its files (OCR stage).</summary>
    /// <returns>The document, or <c>null</c> when the claim is lost and nothing was changed.</returns>
    Task<Document?> StartAsync(JobClaim claim, CancellationToken cancellationToken);

    /// <summary>
    /// Stores the result of a successful OCR attempt and moves the job on to <see cref="JobStage.Separation"/>
    /// for a scanned stack, otherwise to <see cref="JobStage.Recognition"/>, due at once with its attempts reset, in one transaction. A
    /// document without text, without owner or with confirmed fields has nothing to recognize: its job
    /// is removed instead. The page information replaces the document's earlier one in the same transaction.
    /// </summary>
    /// <param name="text">Recognized text; empty when the document has none.</param>
    /// <param name="archive">What the storage wrote for the archive PDF.</param>
    /// <param name="pages">One entry per page of the archive PDF (<see cref="IPageAnalyzer"/>).</param>
    /// <returns><c>false</c> when the claim is lost and nothing was changed.</returns>
    Task<bool> CompleteAsync(
        JobClaim claim, string text, StoredFile archive, IReadOnlyList<PageInfo> pages, CancellationToken cancellationToken);

    /// <summary>
    /// Queues the planned edit of a done document (<see cref="IDocumentEdits"/>, saved before) as a job at
    /// <see cref="JobStage.Edit"/>, due at once with priority 0. A waiting recognition job nobody holds is
    /// taken over: recognition then follows the edit. Joins the caller's open transaction.
    /// </summary>
    /// <param name="recognizeAfter">Recognition follows the edit even without a waiting recognition job.</param>
    /// <returns><c>false</c>, changing nothing, while another job of the document runs or waits (OCR, edit, separation).</returns>
    Task<bool> EnqueueEditAsync(Guid documentId, bool recognizeAfter, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the document's job, whatever its stage, e.g. because the document went to the trash. A
    /// Worker running it loses its claim and discards its result. Joins the caller's open transaction.
    /// </summary>
    /// <returns>Whether there was a job.</returns>
    Task<bool> CancelAsync(Guid documentId, CancellationToken cancellationToken);

    /// <summary>
    /// Queues the OCR of a document that was not done yet (queued or being processed) when it went to the
    /// trash and lost its job, due at once with priority 0. Does nothing for a document that is done, failed,
    /// in the trash or has a job. Joins the caller's open transaction, after the tracked changes are written.
    /// </summary>
    /// <returns>Whether a job was queued.</returns>
    Task<bool> RequeueOcrAsync(Guid documentId, CancellationToken cancellationToken);

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
    /// <param name="wasPrivate">
    /// The document was private when the fields were made, so they come from the owner's private view.
    /// If it is shared by now, checked under the row lock, nothing is taken and the job is handed back
    /// like <see cref="ReleaseAsync"/> (<see cref="RecognitionCommit.SharedMeanwhile"/>).
    /// </param>
    /// <param name="wasOwner">
    /// The owner whose lists the fields were made with. If another account owns the document by now
    /// (handed over), nothing is taken and the job is handed back (<see cref="RecognitionCommit.OwnerChanged"/>).
    /// </param>
    Task<RecognitionCommit> CompleteRecognitionAsync(
        JobClaim claim, DocumentFields? fields, bool wasPrivate, Guid? wasOwner, CancellationToken cancellationToken);

    /// <summary>
    /// Queues recognition of a done document with text again, due at once with priority 0, e.g. after it
    /// was handed over to another account. A job at the recognition stage starts over (a running attempt
    /// loses its claim); a job still at OCR is left alone, recognition follows it anyway. Joins the
    /// caller's open transaction, if any.
    /// </summary>
    /// <returns>Whether a recognition job is queued now.</returns>
    Task<bool> RequeueRecognitionAsync(Guid documentId, CancellationToken cancellationToken);

    /// <summary>
    /// Queues recognition for every done document with text and owner that has no fields
    /// (<see cref="RecognitionState.None"/>) and no job, e.g. documents from before recognition existed.
    /// </summary>
    /// <param name="priority">Priority of the new jobs; a higher number runs after new uploads.</param>
    /// <param name="now">When the new jobs are due.</param>
    /// <returns>How many jobs were queued.</returns>
    Task<int> EnqueueRecognitionBacklogAsync(int priority, DateTimeOffset now, CancellationToken cancellationToken);
}
