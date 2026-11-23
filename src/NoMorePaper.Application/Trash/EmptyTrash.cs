using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Accounts;
using NoMorePaper.Application.Classification;

namespace NoMorePaper.Application.Trash;

/// <summary>
/// The owner empties the trash: every entry of the documents the account owns is removed for good, each in
/// a change of its own, so an entry restored meanwhile stays restored.
/// </summary>
public sealed class EmptyTrash(ITrashStore trash, TrashEraser eraser, ICurrentAccount current, ILogger<EmptyTrash> logger)
{
    private const string Action = "empty-trash";

    /// <returns>
    /// <see cref="ChangeOutcome.Changed"/> (also for an empty trash), or <see cref="ChangeOutcome.AccountGone"/>
    /// if the account was removed; and how many entries were removed. Entries that went with their document
    /// are not counted on their own.
    /// </returns>
    public async Task<(ChangeOutcome Outcome, int Count)> EmptyAsync(CancellationToken cancellationToken)
    {
        if (!await current.ExistsAsync(cancellationToken).ConfigureAwait(false))
        {
            logger.AccountGone(current.Id, Action);
            return (ChangeOutcome.AccountGone, 0);
        }

        var count = 0;
        foreach (var entry in await trash.ListAsync(current.Id, cancellationToken).ConfigureAwait(false))
        {
            if (await eraser.EraseAsync(entry.Item.Id, current.Id, cancellationToken).ConfigureAwait(false) is not null)
            {
                count++;
            }
        }

        logger.TrashEmptied(current.Id, count);
        return (ChangeOutcome.Changed, count);
    }
}
