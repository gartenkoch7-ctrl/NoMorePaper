using NoMorePaper.Application.Processing;
using NoMorePaper.Application.Separation;
using NoMorePaper.Domain.Editing;

namespace NoMorePaper.Infrastructure.Processing;

// The end of the separation stage (M4b, stack separation); OCR sends scanned stacks here in JobQueue.cs.
public sealed partial class JobQueue : ISeparationQueue
{
    /// <inheritdoc />
    public async Task<bool> CompleteSeparationAsync(JobClaim claim, SplitSuggestion? suggestion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claim);
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            // The document first, then the job (see LockDocumentAsync).
            await LockDocumentAsync(claim.DocumentId, cancellationToken).ConfigureAwait(false);
            var job = await LockClaimedJobAsync(claim, cancellationToken).ConfigureAwait(false);
            if (job is null)
            {
                return false;
            }

            var document = await FindDocumentAsync(claim.DocumentId, cancellationToken).ConfigureAwait(false);
            if (suggestion is not null)
            {
                document.SuggestSplit(suggestion);
            }

            MoveOn(job, document, JobStage.Recognition);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
    }
}
