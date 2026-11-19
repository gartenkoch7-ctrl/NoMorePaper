using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Processing;

namespace NoMorePaper.Application.Recognition;

/// <summary>
/// Queues recognition for the documents that have none yet (M3 plan, "Bestand"), at a lower priority
/// than new uploads. Runs once per Worker start; documents queued already are not queued twice.
/// </summary>
public sealed class EnqueueBacklog(IJobQueue queue, TimeProvider time, ILogger<EnqueueBacklog> logger)
{
    /// <summary>Priority of backlog jobs; new uploads have 0 and run first.</summary>
    public const int Priority = 10;

    /// <returns>How many documents were queued.</returns>
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var count = await queue.EnqueueRecognitionBacklogAsync(Priority, time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        logger.BacklogQueued(count);
        return count;
    }
}
