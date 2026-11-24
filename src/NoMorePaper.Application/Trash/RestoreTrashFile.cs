using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Accounts;
using NoMorePaper.Application.Classification;
using NoMorePaper.Application.Documents;
using NoMorePaper.Application.Editing;
using NoMorePaper.Application.Processing;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Trash;

/// <summary>
/// Brings the original or an edited version back to its document (part of <see cref="RestoreTrashItem"/>).
/// An edited version takes the place of the one the document keeps now, which goes to the trash instead
/// (swap), and the archive is made anew from it (<see cref="EditKind.Rebuild"/>). A document that should keep
/// only the other version keeps both again. The files move while the document row is locked, before the
/// commit; if the commit fails they move back, so the stored files always match the database.
/// </summary>
public sealed class RestoreTrashFile(
    ITrashStore trash,
    IDocumentFieldsStore documents,
    IDocumentStorage storage,
    IJobQueue queue,
    IDocumentEdits edits,
    IAccountPreferencesStore preferences,
    ICurrentAccount current,
    TimeProvider time,
    ILogger<RestoreTrashFile> logger)
{
    private const string Action = "restore";

    /// <param name="item">A file entry of a document the signed-in account owns, its document row locked (<see cref="ITrashStore.FindLockedAsync"/>).</param>
    internal async Task<ChangeOutcome> RestoreAsync(TrashItem item, CancellationToken cancellationToken)
    {
        var file = item.File ?? throw new ArgumentException("Only a file entry.", nameof(item));
        var document = await documents.FindVisibleAsync(item.DocumentId, current.Id, cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            // Hidden because it lies in the trash itself: the file can only come back with its document.
            var inTrash = await trash.FindTrashedDocumentAsync(item.DocumentId, current.Id, cancellationToken).ConfigureAwait(false) is not null;
            return Refuse(item, inTrash ? ChangeOutcome.Conflict : ChangeOutcome.NotFound, inTrash ? "document-in-trash" : "no-document");
        }

        var present = document.FindFile(file.Kind);
        if (document.State != ProcessingState.Done || (file.Kind == DocumentFileKind.Original && present is not null))
        {
            return Refuse(item, ChangeOutcome.Conflict, "not-done-or-taken");
        }

        // An edit of the document moves its files after its commit, without the lock: a file put back now
        // could be moved away under it or left where nothing points to it. The owner tries again afterwards.
        if (await edits.GetAsync(document.Id, cancellationToken).ConfigureAwait(false) is not null)
        {
            return Refuse(item, ChangeOutcome.Conflict, "job-pending");
        }

        var now = time.GetUtcNow();
        if (file.Kind == DocumentFileKind.Edited)
        {
            // Queued first: while another job of the document runs or waits, nothing may change its files. The
            // planned edit needs to exist for the queue; a refusal rolls it back with the scope, never committed.
            await edits.SaveAsync(new DocumentEdit(document.Id, EditKind.Rebuild, "{}", current.Id, now, false), cancellationToken)
                .ConfigureAwait(false);
            if (!await queue.EnqueueEditAsync(document.Id, recognizeAfter: false, cancellationToken).ConfigureAwait(false))
            {
                return Refuse(item, ChangeOutcome.Conflict, "job-pending");
            }

            document.ReplaceFile(file, now);
        }
        else
        {
            document.AttachFile(file);
        }

        await KeepBothIfNeededAsync(document, item, cancellationToken).ConfigureAwait(false);
        var swapped = present is null ? null : TrashItem.ForFile(document, present, now);
        if (swapped is not null)
        {
            await trash.AddAsync(swapped, cancellationToken).ConfigureAwait(false);
        }

        await trash.RemoveAsync(item, cancellationToken).ConfigureAwait(false);
        await MoveAndCommitAsync(document.Id, item, swapped, cancellationToken).ConfigureAwait(false);

        logger.EntryRestored(item.Id, item.Kind, document.Id, current.Id);
        if (swapped is not null)
        {
            logger.VersionSwapped(document.Id, swapped.Id);
        }

        if (file.Kind == DocumentFileKind.Edited)
        {
            logger.RebuildQueued(document.Id);
        }

        return ChangeOutcome.Changed;
    }

    // A version that comes back is meant to be kept: a choice to keep only the other one (on the document,
    // else the owner's default) would drop it again with the next edit.
    private async Task KeepBothIfNeededAsync(Document document, TrashItem item, CancellationToken cancellationToken)
    {
        var policy = document.VersionPolicy ?? await preferences.GetAfterEditingAsync(current.Id, cancellationToken).ConfigureAwait(false);
        var dropsIt = item.Kind == TrashItemKind.Original ? policy == VersionPolicy.EditedOnly : policy == VersionPolicy.OriginalOnly;
        if (dropsIt)
        {
            document.ChooseVersions(current.Id, VersionPolicy.KeepBoth);
            logger.BothVersionsKept(document.Id, item.Kind);
        }
    }

    private async Task MoveAndCommitAsync(Guid documentId, TrashItem item, TrashItem? swapped, CancellationToken cancellationToken)
    {
        var kind = item.File!.Kind;
        cancellationToken.ThrowIfCancellationRequested();

        // From the first move on, the restore is finished even if the request is dropped.
        var undo = new Stack<Func<Task>>();
        try
        {
            if (swapped is not null)
            {
                await storage.MoveToTrashAsync(documentId, kind, swapped.Id, CancellationToken.None).ConfigureAwait(false);
                undo.Push(() => storage.RestoreFromTrashAsync(swapped.Id, documentId, kind, CancellationToken.None));
            }

            await storage.RestoreFromTrashAsync(item.Id, documentId, kind, CancellationToken.None).ConfigureAwait(false);
            undo.Push(() => storage.MoveToTrashAsync(documentId, kind, item.Id, CancellationToken.None));
            await trash.SaveAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await UndoAsync(undo, item.Id, documentId, exception).ConfigureAwait(false);
            throw;
        }
    }

    private async Task UndoAsync(Stack<Func<Task>> undo, Guid itemId, Guid documentId, Exception failure)
    {
        try
        {
            while (undo.TryPop(out var step))
            {
                await step().ConfigureAwait(false);
            }

            logger.RestoreUndone(itemId, documentId, failure.GetType().Name);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.RestoreUndoFailed(itemId, documentId, exception.GetType().Name);
        }
    }

    private ChangeOutcome Refuse(TrashItem item, ChangeOutcome outcome, string reason)
    {
        logger.TrashChangeRefused(current.Id, outcome, Action, item.Id, reason);
        return outcome;
    }
}
