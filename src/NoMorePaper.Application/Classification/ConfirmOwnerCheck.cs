using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Accounts;

namespace NoMorePaper.Application.Classification;

/// <summary>
/// "Fits": the owner of a document whose owner was unknown on arrival keeps it as it is. It leaves
/// "to review" for this reason and stays private or shared as it is now.
/// </summary>
public sealed class ConfirmOwnerCheck(IDocumentFieldsStore documents, ICurrentAccount current, ILogger<ConfirmOwnerCheck> logger)
{
    private const string Action = "confirm-owner";

    /// <returns><see cref="ChangeOutcome.Conflict"/> if the document does not wait for its owner's check.</returns>
    public async Task<ChangeOutcome> ConfirmAsync(Guid id, CancellationToken cancellationToken)
    {
        // Loaded under the row lock of FindVisibleAsync, like every change of M3.
        var (document, outcome) = await DocumentChangeGuard.LoadAsync(
            documents, current, logger, id, Action, found => found.OwnerId == current.Id, cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return outcome;
        }

        if (!document.NeedsOwnerCheck)
        {
            logger.DocumentChangeRefused(current.Id, ChangeOutcome.Conflict, Action, id);
            return ChangeOutcome.Conflict;
        }

        document.ConfirmOwnerCheck(current.Id);
        await documents.SaveAsync(cancellationToken).ConfigureAwait(false);
        logger.OwnerCheckConfirmed(id, current.Id);
        return ChangeOutcome.Changed;
    }
}
