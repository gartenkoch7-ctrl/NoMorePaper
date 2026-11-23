using Microsoft.Extensions.Logging;

namespace NoMorePaper.Application.Trash;

/// <summary>
/// The daily purge of the Worker (ADR-0025): every entry whose 30 days are up is removed for good, of every
/// account, oldest first, like the owner would remove it (<see cref="TrashEraser"/>). A file whose document
/// has an edit planned or running stays until a later run.
/// </summary>
public sealed class PurgeDueTrash(ITrashStore trash, TrashEraser eraser, TimeProvider time, ILogger<PurgeDueTrash> logger)
{
    /// <summary>Entries read per round; a long backlog takes several rounds of one run.</summary>
    public const int BatchSize = 200;

    /// <returns>How many entries were removed; entries that went with their document are not counted on their own.</returns>
    public async Task<int> PurgeAsync(CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var total = 0;
        while (true)
        {
            var due = await trash.DueAsync(now, BatchSize, cancellationToken).ConfigureAwait(false);
            var removed = 0;
            foreach (var item in due)
            {
                if (await eraser.EraseAsync(item.Id, ownerId: null, cancellationToken).ConfigureAwait(false) == EraseOutcome.Erased)
                {
                    removed++;
                }
            }

            total += removed;

            // A full round that removed nothing would read the same entries again.
            if (due.Count < BatchSize || removed == 0)
            {
                break;
            }
        }

        logger.DueTrashPurged(total);
        return total;
    }
}
