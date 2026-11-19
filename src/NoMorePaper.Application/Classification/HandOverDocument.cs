using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Accounts;
using NoMorePaper.Application.Documents;
using NoMorePaper.Application.Processing;
using NoMorePaper.Domain.Classification;
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

        // Confirmed fields stay, but only with entries the new view sees: the new owner's when the
        // document stays private, the household's when it becomes shared (as recognition chooses).
        var audience = wanted.NewDocumentsPrivate ? EntryAudience.Account : EntryAudience.Household;
        var hidden = recognizeAgain ? new List<Guid>() : await HiddenEntriesAsync(document, newOwnerId, audience, cancellationToken).ConfigureAwait(false);
        document.HandOver(current.Id, newOwnerId, wanted.NewDocumentsPrivate, hidden);

        // Same transaction as the hand-over (the store's, still open): a Worker sees the new owner and
        // the new job together, or neither.
        var requeued = recognizeAgain && await queue.RequeueRecognitionAsync(id, cancellationToken).ConfigureAwait(false);
        await documents.SaveAsync(cancellationToken).ConfigureAwait(false);
        logger.HandedOver(current.Id, id, newOwnerId, requeued, hidden.Count);
        return ChangeOutcome.Changed;
    }

    // Asked before the hand-over is saved: the document still counts as the previous owner's private one,
    // so it does not make its own entries visible to the new view.
    private async Task<List<Guid>> HiddenEntriesAsync(Document document, Guid newOwnerId, EntryAudience audience, CancellationToken cancellationToken)
    {
        var used = new List<(ClassificationKind Kind, Guid Id)>();
        if (document.CorrespondentId is { } sender)
        {
            used.Add((ClassificationKind.Correspondent, sender));
        }

        if (document.DocumentTypeId is { } type)
        {
            used.Add((ClassificationKind.DocumentType, type));
        }

        used.AddRange(document.Tags.Select(tag => (ClassificationKind.Tag, tag.TagId)));
        var hidden = new List<Guid>();
        foreach (var (kind, entryId) in used)
        {
            if (!await documents.AreVisibleAsync(newOwnerId, kind, [entryId], audience, cancellationToken).ConfigureAwait(false))
            {
                hidden.Add(entryId);
            }
        }

        return hidden;
    }
}
