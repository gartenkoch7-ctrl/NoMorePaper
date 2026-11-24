using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Processing;

namespace NoMorePaper.Application.Editing;

/// <summary>
/// Queues the page information for documents processed before M4b (question F1, answered "nachholen"):
/// only reading text and a small picture per page, no OCR, after every other job. Runs once per Worker
/// start; documents queued already are not queued twice.
/// </summary>
public sealed class EnqueuePageBacklog(IJobQueue queue, TimeProvider time, ILogger<EnqueuePageBacklog> logger)
{
    /// <summary>Priority of backlog jobs: after new uploads (0) and the recognition backlog (10).</summary>
    public const int Priority = 20;

    /// <returns>How many documents were queued.</returns>
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var count = await queue.EnqueuePageBacklogAsync(Priority, time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        logger.PageBacklogQueued(count);
        return count;
    }
}
