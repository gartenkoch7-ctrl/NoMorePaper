using System.Diagnostics.CodeAnalysis;
using NoMorePaper.Application.Processing;
using NoMorePaper.Domain.Editing;

namespace NoMorePaper.Application.Separation;

/// <summary>
/// The end of the <see cref="JobStage.Separation"/> stage in the processing queue. Apart from
/// <see cref="IJobQueue"/>, which the edit stages extend at the same time (M4b tracks A and D).
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "A queue in the domain sense, like IJobQueue.")]
public interface ISeparationQueue
{
    /// <summary>
    /// Ends the separation stage in one transaction: notes <paramref name="suggestion"/> on the document and
    /// moves the job on to <see cref="JobStage.Recognition"/>, due at once with its attempts reset, or removes
    /// it when there is nothing to recognize (no text, no owner, confirmed fields), like the OCR stage does.
    /// </summary>
    /// <param name="suggestion">Where the stack probably splits; <c>null</c> leaves the document as it is.</param>
    /// <returns><c>false</c> when the claim is lost and nothing was changed.</returns>
    Task<bool> CompleteSeparationAsync(JobClaim claim, SplitSuggestion? suggestion, CancellationToken cancellationToken);
}
