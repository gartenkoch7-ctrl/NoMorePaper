using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Accounts;
using NoMorePaper.Application.Classification;
using NoMorePaper.Application.Processing;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Domain.Editing;

namespace NoMorePaper.Application.Editing;

/// <summary>
/// "Dokument anhängen" (ADR-0025): the pages of another document go to the end of the open one, which
/// keeps its fields, owner and privacy; the appended one goes to the trash when the Worker has done it.
/// When the appended document is private and the open one shared, the result is private at once (the app
/// said so beforehand); only the owner of the open one may make it so.
/// </summary>
public sealed class AppendDocument(
    IDocumentFieldsStore documents,
    IDocumentEdits edits,
    IJobQueue queue,
    ICurrentAccount current,
    TimeProvider time,
    ILogger<AppendDocument> logger)
{
    private const string Action = "append";

    /// <returns>
    /// <see cref="ChangeOutcome.Changed"/> when queued; <see cref="ChangeOutcome.Invalid"/> for the document itself;
    /// <see cref="ChangeOutcome.NotFound"/> if the account may not see one of them; <see cref="ChangeOutcome.Forbidden"/>
    /// if it may not edit one of them (or make the result private); <see cref="ChangeOutcome.Conflict"/> while one is not
    /// done, lies in the trash or has an edit or job pending.
    /// </returns>
    public async Task<ChangeOutcome> AppendAsync(Guid id, Guid appendedId, CancellationToken cancellationToken)
    {
        if (id == appendedId)
        {
            return Refuse(ChangeOutcome.Invalid, id);
        }

        if (!await current.ExistsAsync(cancellationToken).ConfigureAwait(false))
        {
            logger.AccountGone(current.Id, Action);
            return ChangeOutcome.AccountGone;
        }

        // Both rows are locked in the order of their ids, as the Worker locks them when it stores the result.
        var first = await documents.FindVisibleAsync(Min(id, appendedId), current.Id, cancellationToken).ConfigureAwait(false);
        var second = await documents.FindVisibleAsync(Max(id, appendedId), current.Id, cancellationToken).ConfigureAwait(false);
        var (target, appended) = id.CompareTo(appendedId) < 0 ? (first, second) : (second, first);
        if (target is null || appended is null)
        {
            return Refuse(ChangeOutcome.NotFound, id);
        }

        var refusal = AppendRule.Check(target, appended, current.Id) switch
        {
            null => (ChangeOutcome?)null,
            AppendRefusal.Same => ChangeOutcome.Invalid,
            AppendRefusal.NotEditable => ChangeOutcome.Forbidden,
            _ => ChangeOutcome.Conflict,
        };
        if (refusal is { } refused)
        {
            return Refuse(refused, id);
        }

        // An edit of the appended document would change the pages the Worker is about to take.
        if (await edits.GetAsync(appendedId, cancellationToken).ConfigureAwait(false) is not null)
        {
            return Refuse(ChangeOutcome.Conflict, id);
        }

        var makePrivate = AppendRule.ResultIsPrivate(target, appended) && !target.IsPrivate;
        if (makePrivate && target.OwnerId != current.Id)
        {
            return Refuse(ChangeOutcome.Forbidden, id);
        }

        var now = time.GetUtcNow();
        var data = new EditData(Guid.CreateVersion7(now)) { AppendedId = appendedId };
        if (!await EditRequests.QueueAsync(queue, edits, logger, id, EditKind.Append, data, current.Id, now, cancellationToken).ConfigureAwait(false))
        {
            return ChangeOutcome.Conflict;
        }

        if (makePrivate)
        {
            target.MakePrivate(current.Id);
        }

        await documents.SaveAsync(cancellationToken).ConfigureAwait(false);
        logger.EditRequested(current.Id, EditKind.Append, id);
        return ChangeOutcome.Changed;
    }

    private ChangeOutcome Refuse(ChangeOutcome outcome, Guid id)
    {
        logger.DocumentChangeRefused(current.Id, outcome, Action, id);
        return outcome;
    }

    private static Guid Min(Guid a, Guid b) => a.CompareTo(b) < 0 ? a : b;

    private static Guid Max(Guid a, Guid b) => a.CompareTo(b) < 0 ? b : a;
}
