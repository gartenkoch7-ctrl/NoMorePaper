using NoMorePaper.Application.Classification;
using NoMorePaper.Domain.Editing;

namespace NoMorePaper.Application.Editing;

/// <summary>
/// Asks for a document to be split by the signed-in account (implemented in M4b track A, used by the
/// split suggestion of track D): checks the rights and the plan and queues the edit.
/// </summary>
public interface ISplitRequester
{
    /// <returns>
    /// <see cref="ChangeOutcome.Changed"/> when the split is queued and committed; <see cref="ChangeOutcome.Invalid"/> for a
    /// plan that does not fit; <see cref="ChangeOutcome.Conflict"/> while the document is not done or another edit or job
    /// runs or waits. A refusal commits nothing and leaves the open change (the document's row lock) as it found it, so
    /// the caller needs no compensation: committing the change afterwards stores only the caller's own changes.
    /// </returns>
    Task<ChangeOutcome> RequestAsync(Guid id, SplitPlan plan, CancellationToken cancellationToken);
}
