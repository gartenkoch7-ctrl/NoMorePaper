using System.Diagnostics.CodeAnalysis;
using NoMorePaper.Application.Documents;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Domain.Processing;

namespace NoMorePaper.Application.Processing;

/// <summary>
/// The processing queue (docs/kb/verarbeitung.md). Workers claim the earliest due job, so several
/// Workers never get the same one, and hold it until the lock expires; a crashed Worker's job
/// becomes due again after that.
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
    /// <summary>Claims the earliest due job that nobody holds, counting it as a new attempt.</summary>
    /// <param name="lockFor">How long the job stays claimed; longer than processing may take.</param>
    /// <returns>The claim, or <c>null</c> when no job is due.</returns>
    Task<JobClaim?> ClaimNextAsync(TimeSpan lockFor, CancellationToken cancellationToken);

    /// <summary>Marks the document as being processed and returns it with its files.</summary>
    /// <returns>The document, or <c>null</c> when the claim is lost and nothing was changed.</returns>
    Task<Document?> StartAsync(JobClaim claim, CancellationToken cancellationToken);

    /// <summary>Stores the result of a successful attempt and removes the job, in one transaction.</summary>
    /// <param name="text">Recognized text; empty when the document has none.</param>
    /// <param name="archive">What the storage wrote for the archive PDF.</param>
    /// <returns><c>false</c> when the claim is lost and nothing was changed.</returns>
    Task<bool> CompleteAsync(JobClaim claim, string text, StoredFile archive, CancellationToken cancellationToken);

    /// <summary>
    /// Records a failed attempt: schedules the next one after the <see cref="RetryPolicy"/> delay,
    /// or, when all attempts are used up or the failure is permanent, marks the document as failed
    /// and removes the job.
    /// </summary>
    /// <param name="technicalError">Technical reason without document content; cut to <see cref="Document.MaxFailureReasonLength"/>.</param>
    /// <param name="permanent">Another attempt would fail the same way, so the document fails at once.</param>
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
}
