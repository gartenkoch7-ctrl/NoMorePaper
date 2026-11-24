using NoMorePaper.Application.Trash;

namespace NoMorePaper.Worker.Trash;

/// <summary>
/// The daily purge of the trash (ADR-0025): at start and then every <see cref="Interval"/>, removes for good
/// every entry whose 30 days are up (<see cref="PurgeDueTrash"/>), in a scope of its own. A failed run is
/// logged and tried again at the next time; it never stops the Worker host.
/// </summary>
internal sealed class TrashPurger(IServiceScopeFactory scopes, TimeProvider time, ILogger<TrashPurger> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.PurgerStarted((int)Interval.TotalHours);
        using var timer = new PeriodicTimer(Interval, time);
        try
        {
            do
            {
                await RunOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Stopping.
        }
    }

    /// <summary>One run of the purge.</summary>
    /// <returns>How many entries were removed, or <c>null</c> when the run failed.</returns>
    /// <exception cref="OperationCanceledException">The Worker is stopping.</exception>
    internal async Task<int?> RunOnceAsync(CancellationToken stoppingToken)
    {
        var started = time.GetTimestamp();
        try
        {
            var scope = scopes.CreateAsyncScope();
            await using (scope.ConfigureAwait(false))
            {
                var count = await scope.ServiceProvider.GetRequiredService<PurgeDueTrash>().PurgeAsync(stoppingToken).ConfigureAwait(false);
                var elapsedMs = (long)time.GetElapsedTime(started).TotalMilliseconds;
                logger.PurgeRan(count, elapsedMs);
                return count;
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // E.g. the database is not reachable yet; the entries stay due and go at the next run.
            logger.PurgeFailed(exception.GetType().Name);
            return null;
        }
    }
}
