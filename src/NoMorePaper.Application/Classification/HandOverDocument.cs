using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Accounts;
using NoMorePaper.Application.Documents;
using NoMorePaper.Application.Processing;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Classification;

/// <summary>
/// "Hand over": the owner of a document whose owner was unknown on arrival gives it to the account it
/// belongs to. The new owner's preference decides whether it is private. Fields nobody confirmed are
/// dropped and recognition runs again with the new owner's view, queued in the same transaction:
/// otherwise the previous owner's private list entries could reach the new owner (ADR-0023, ADR-0024).
/// </summary>
public sealed class HandOverDocument(
    IDocumentFieldsStore documents,
    IAccountStore accounts,
    IAccountPreferencesStore preferences,
    IJobQueue queue,
    ICurrentAccount current,
    ILogger<HandOverDocument> logger)
{
    private const string Action = "hand-over";

    /// <param name="newOwnerId">An existing account other than the signed-in one; otherwise <see cref="ChangeOutcome.Invalid"/>.</param>
    /// <returns><see cref="ChangeOutcome.Conflict"/> if the document does not wait for its owner's check.</returns>
    public async Task<ChangeOutcome> HandOverAsync(Guid id, Guid newOwnerId, CancellationToken cancellationToken)
    {
        var (document, outcome) = await DocumentChangeGuard.LoadAsync(
            documents, current, logger, id, Action, found => found.OwnerId == current.Id, cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return outcome;
        }

        if (newOwnerId == current.Id || !await accounts.ExistsAsync(newOwnerId, cancellationToken).ConfigureAwait(false))
        {
            logger.DocumentChangeRefused(current.Id, ChangeOutcome.Invalid, Action, id);
            return ChangeOutcome.Invalid;
        }

        if (!document.NeedsOwnerCheck)
        {
            logger.DocumentChangeRefused(current.Id, ChangeOutcome.Conflict, Action, id);
            return ChangeOutcome.Conflict;
        }

        var wanted = await preferences.GetAsync(newOwnerId, cancellationToken).ConfigureAwait(false);
        var recognizeAgain = document.Recognition != RecognitionState.Confirmed;
        document.HandOver(current.Id, newOwnerId, wanted.NewDocumentsPrivate);

        // Same transaction as the hand-over (the store's, still open): a Worker sees the new owner and
        // the new job together, or neither.
        var requeued = recognizeAgain && await queue.RequeueRecognitionAsync(id, cancellationToken).ConfigureAwait(false);
        await documents.SaveAsync(cancellationToken).ConfigureAwait(false);
        logger.HandedOver(current.Id, id, newOwnerId, requeued);
        return ChangeOutcome.Changed;
    }
}
