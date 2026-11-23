using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Accounts;
using NoMorePaper.Application.Classification;

namespace NoMorePaper.Application.Trash;

/// <summary>
/// The owner removes an entry of the trash for good (ADR-0025): a file, or a whole document with its files
/// and every other trash entry of it. It cannot be restored afterwards.
/// </summary>
public sealed class PurgeTrashItem(TrashEraser eraser, ICurrentAccount current, ILogger<PurgeTrashItem> logger)
{
    private const string Action = "purge";

    /// <returns>
    /// <see cref="ChangeOutcome.Changed"/>; <see cref="ChangeOutcome.NotFound"/> if there is no such entry of a
    /// document the account owns; <see cref="ChangeOutcome.AccountGone"/> if the account was removed.
    /// </returns>
    public async Task<ChangeOutcome> PurgeAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await current.ExistsAsync(cancellationToken).ConfigureAwait(false))
        {
            logger.AccountGone(current.Id, Action);
            return ChangeOutcome.AccountGone;
        }

        if (await eraser.EraseAsync(id, current.Id, cancellationToken).ConfigureAwait(false) is null)
        {
            logger.TrashChangeRefused(current.Id, ChangeOutcome.NotFound, Action, id, "no-entry");
            return ChangeOutcome.NotFound;
        }

        logger.EntryPurged(id, current.Id);
        return ChangeOutcome.Changed;
    }
}
