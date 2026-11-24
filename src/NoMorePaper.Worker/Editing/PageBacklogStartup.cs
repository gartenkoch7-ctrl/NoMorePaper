using NoMorePaper.Application.Editing;

namespace NoMorePaper.Worker.Editing;

/// <summary>
/// Once per Worker start: queues the page information of documents from before M4b
/// (<see cref="EnqueuePageBacklog"/>). Retried until it succeeds, because the database may start later
/// than the Worker; documents are processed meanwhile.
/// </summary>
internal sealed class PageBacklogStartup(IServiceScopeFactory scopes, TimeProvider time, ILogger<PageBacklogStartup> logger) : BackgroundService
{
    /// <summary>Waits between tries: 30 s at first, doubled up to 10 min.</summary>
    public static readonly TimeSpan FirstDelay = TimeSpan.FromSeconds(30);

    public static readonly TimeSpan MaxDelay = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = FirstDelay;
        while (true)
        {
            try
            {
                var scope = scopes.CreateAsyncScope();
                await using (scope.ConfigureAwait(false))
                {
                    await scope.ServiceProvider.GetRequiredService<EnqueuePageBacklog>().RunAsync(stoppingToken).ConfigureAwait(false);
                    return;
                }
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.PageBacklogNotQueued(exception.GetType().Name, (int)delay.TotalSeconds);
            }

            await Task.Delay(delay, time, stoppingToken).ConfigureAwait(false);
            delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, MaxDelay.Ticks));
        }
    }
}
