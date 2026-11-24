using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Accounts;
using NoMorePaper.Application.Classification;
using NoMorePaper.Application.Documents;
using NoMorePaper.Application.Processing;
using NoMorePaper.Application.Trash;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Editing;

/// <summary>
/// The owner chooses which versions a document keeps (ADR-0025, "Fassungen"). "Only edited" moves the
/// original to the trash; "only original" moves the edited version there and queues a new archive made
/// from the original (<see cref="EditKind.Rebuild"/>; the viewer shows the old one until it is done);
/// "both" only records the choice, as does any choice for a document without edited version: it applies
/// to its next edit. The entry and the file change are committed together; the file follows on disk after.
/// </summary>
public sealed class ChooseVersions(
    IDocumentFieldsStore documents,
    ITrashStore trash,
    IDocumentEdits edits,
    IJobQueue queue,
    IDocumentStorage storage,
    ICurrentAccount current,
    TimeProvider time,
    ILogger<ChooseVersions> logger)
{
    private const string Action = "choose-versions";

    /// <returns>
    /// <see cref="ChangeOutcome.Changed"/>; <see cref="ChangeOutcome.Invalid"/> for an unknown policy; <see cref="ChangeOutcome.NotFound"/>
    /// or <see cref="ChangeOutcome.Forbidden"/> unless the account owns it; <see cref="ChangeOutcome.Conflict"/> while it is not
    /// done, an edit is pending, or "only original" is chosen without original.
    /// </returns>
    public async Task<ChangeOutcome> ChooseAsync(Guid id, VersionPolicy policy, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(policy))
        {
            return ChangeOutcome.Invalid;
        }

        var (document, outcome) = await DocumentChangeGuard.LoadAsync(
            documents, current, logger, id, Action, found => found.OwnerId == current.Id, cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return outcome;
        }

        var edited = document.FindFile(DocumentFileKind.Edited);
        var original = document.FindFile(DocumentFileKind.Original);
        var conflict = document.State != ProcessingState.Done
            || await edits.GetAsync(id, cancellationToken).ConfigureAwait(false) is not null
            || (policy == VersionPolicy.OriginalOnly && edited is not null && original is null);
        if (conflict)
        {
            logger.DocumentChangeRefused(current.Id, ChangeOutcome.Conflict, Action, id);
            return ChangeOutcome.Conflict;
        }

        var now = time.GetUtcNow();
        var dropped = (policy, edited, original) switch
        {
            (VersionPolicy.EditedOnly, not null, { } file) => file,
            (VersionPolicy.OriginalOnly, { } file, not null) => file,
            _ => null,
        };
        // Queued first: a refusal leaves the document untouched.
        if (dropped?.Kind == DocumentFileKind.Edited)
        {
            var data = new EditData(Guid.CreateVersion7(now));
            if (!await EditRequests.QueueAsync(queue, edits, logger, id, EditKind.Rebuild, data, current.Id, now, cancellationToken).ConfigureAwait(false))
            {
                return ChangeOutcome.Conflict;
            }
        }

        TrashItem? item = null;
        if (dropped is not null)
        {
            item = TrashItem.ForFile(document, dropped, now);
            document.RemoveFile(dropped.Kind);
        }

        document.ChooseVersions(current.Id, policy);
        if (item is not null)
        {
            await trash.AddAsync(item, cancellationToken).ConfigureAwait(false);
        }

        await documents.SaveAsync(cancellationToken).ConfigureAwait(false);
        if (item is not null)
        {
            // A dropped request must not stop the move of a committed change half way.
            await MoveFileAsync(id, item, CancellationToken.None).ConfigureAwait(false);
        }

        if (dropped?.Kind == DocumentFileKind.Edited)
        {
            logger.RebuildQueued(current.Id, policy, id);
        }
        else
        {
            logger.VersionsChosen(current.Id, policy, id);
        }

        return ChangeOutcome.Changed;
    }

    // After the commit: the entry exists, so a file that cannot move now stays where it is, unused, and the
    // trash shows the entry; restoring it finds the file in place.
    private async Task MoveFileAsync(Guid id, TrashItem item, CancellationToken cancellationToken)
    {
        var kind = item.File!.Kind;
        try
        {
            await storage.MoveToTrashAsync(id, kind, item.Id, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.TrashMoveFailed(kind, id, item.Id, exception.GetType().Name);
        }
    }
}
