using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Accounts;

namespace NoMorePaper.Application.Classification;

/// <summary>
/// The owner chooses the members who may edit a document's fields. Replaces the list; only members of
/// the household can be chosen. Editors of a private document can edit it only once it is shared again.
/// </summary>
public sealed class SetDocumentEditors(
    IDocumentFieldsStore documents, IAccountStore accounts, ICurrentAccount current, ILogger<SetDocumentEditors> logger)
{
    private const string Action = "set-editors";

    /// <param name="editorIds">All editors; <c>null</c> is invalid, so editors are never removed by accident.</param>
    public async Task<ChangeOutcome> SetAsync(Guid id, IReadOnlyList<Guid>? editorIds, CancellationToken cancellationToken)
    {
        if (editorIds is null)
        {
            return ChangeOutcome.Invalid;
        }

        var (document, outcome) = await DocumentChangeGuard.LoadAsync(
            documents, current, logger, id, Action, found => found.OwnerId == current.Id, cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return outcome;
        }

        var members = (await accounts.ListAsync(cancellationToken).ConfigureAwait(false)).Select(member => member.Id).ToHashSet();
        if (!editorIds.All(members.Contains))
        {
            logger.DocumentChangeRefused(current.Id, ChangeOutcome.Invalid, Action, id);
            return ChangeOutcome.Invalid;
        }

        document.SetEditors(current.Id, editorIds);
        await documents.SaveAsync(cancellationToken).ConfigureAwait(false);
        logger.EditorsSet(id, current.Id, document.Editors.Count);
        return ChangeOutcome.Changed;
    }
}
