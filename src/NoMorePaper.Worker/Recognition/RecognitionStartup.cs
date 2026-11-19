using NoMorePaper.Application.Recognition;

namespace NoMorePaper.Worker.Recognition;

/// <summary>
/// Once per Worker start: makes sure the LLM has its model (downloading it the first time), then lets
/// recognition jobs be claimed (<see cref="RecognitionReadiness"/>) and queues the recognition backlog.
/// In that order, so no recognition job uses up its attempts while the model is still downloading. Both
/// steps are retried until they succeed, because the Ollama service or the database may start later
/// than the Worker; OCR runs meanwhile.
/// </summary>
internal sealed class RecognitionStartup(
    IRecognizer recognizer,
    RecognitionReadiness readiness,
    IServiceScopeFactory scopes,
    TimeProvider time,
    ILogger<RecognitionStartup> logger) : BackgroundService
{
    /// <summary>Waits between tries: 30 s at first, doubled up to 10 min.</summary>
    public static readonly TimeSpan FirstDelay = TimeSpan.FromSeconds(30);

    public static readonly TimeSpan MaxDelay = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.PreparingModel();
        var started = time.GetTimestamp();
        await RetryAsync(recognizer.PrepareAsync, logger.ModelNotReady, stoppingToken).ConfigureAwait(false);
        var elapsed = time.GetElapsedTime(started);
        logger.ModelReady((long)elapsed.TotalMilliseconds);
        readiness.MarkReady();

        await RetryAsync(EnqueueBacklogAsync, logger.BacklogNotQueued, stoppingToken).ConfigureAwait(false);
    }

    private async Task EnqueueBacklogAsync(CancellationToken stoppingToken)
    {
        var scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            await scope.ServiceProvider.GetRequiredService<EnqueueBacklog>().RunAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task RetryAsync(Func<CancellationToken, Task> step, Action<string, int> logRetry, CancellationToken stoppingToken)
    {
        var delay = FirstDelay;
        while (true)
        {
            try
            {
                await step(stoppingToken).ConfigureAwait(false);
                return;
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logRetry(exception.GetType().Name, (int)delay.TotalSeconds);
            }

            await Task.Delay(delay, time, stoppingToken).ConfigureAwait(false);
            delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, MaxDelay.Ticks));
        }
    }
}
