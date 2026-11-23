using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Accounts;
using NoMorePaper.Application.Classification;
using NoMorePaper.Application.Editing;

namespace NoMorePaper.Application.Separation;

/// <summary>
/// The answer to a split suggestion in "to review": accepting splits the stack as suggested (through
/// <see cref="ISplitRequester"/>, the split of M4b track A), dismissing keeps it as one document. Either
/// is allowed to whoever may edit the document, like splitting by hand; either ends the suggestion.
/// </summary>
public sealed class AnswerSplitSuggestion(
    IDocumentFieldsStore documents,
    ISplitRequester splitter,
    ICurrentAccount current,
    ILogger<AnswerSplitSuggestion> logger)
{
    private const string AcceptAction = "accept-split";
    private const string DismissAction = "dismiss-split";

    /// <returns>
    /// <see cref="ChangeOutcome.Changed"/> when the split is queued; <see cref="ChangeOutcome.Conflict"/> without
    /// a suggestion, when it no longer fits the pages, while the document is not done or another job runs.
    /// </returns>
    public async Task<ChangeOutcome> AcceptAsync(Guid id, CancellationToken cancellationToken)
    {
        // Loaded under the row lock of FindVisibleAsync; the split loads the same document in the same
        // transaction and saves both changes together.
        var (document, outcome) = await DocumentChangeGuard.LoadAsync(
            documents, current, logger, id, AcceptAction, found => found.CanEdit(current.Id), cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return outcome;
        }

        if (document.SplitSuggestion is not { } suggestion)
        {
            logger.DocumentChangeRefused(current.Id, ChangeOutcome.Conflict, AcceptAction, id);
            return ChangeOutcome.Conflict;
        }

        // The suggestion is left untouched while the split is asked for: a refused split changes nothing.
        var plan = suggestion.ToPlan();
        var split = await splitter.RequestAsync(id, plan, cancellationToken).ConfigureAwait(false);
        if (split != ChangeOutcome.Changed)
        {
            // The plan comes from the stored suggestion, not from the request: when it does not fit, the
            // document changed since (pages edited), which is a conflict, not a bad request.
            return split == ChangeOutcome.Invalid ? ChangeOutcome.Conflict : split;
        }

        // Saved in the same transaction as the queued split, so the document leaves "to review" with it.
        document.DismissSplit();
        await documents.SaveAsync(cancellationToken).ConfigureAwait(false);
        logger.SplitAccepted(current.Id, id, plan.Parts.Count);
        return ChangeOutcome.Changed;
    }

    /// <returns><see cref="ChangeOutcome.Conflict"/> when the document has no suggestion.</returns>
    public async Task<ChangeOutcome> DismissAsync(Guid id, CancellationToken cancellationToken)
    {
        var (document, outcome) = await DocumentChangeGuard.LoadAsync(
            documents, current, logger, id, DismissAction, found => found.CanEdit(current.Id), cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return outcome;
        }

        if (document.SplitSuggestion is null)
        {
            logger.DocumentChangeRefused(current.Id, ChangeOutcome.Conflict, DismissAction, id);
            return ChangeOutcome.Conflict;
        }

        document.DismissSplit();
        await documents.SaveAsync(cancellationToken).ConfigureAwait(false);
        logger.SplitDismissed(current.Id, id);
        return ChangeOutcome.Changed;
    }
}
