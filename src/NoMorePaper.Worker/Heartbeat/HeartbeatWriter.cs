using System.Diagnostics;
using NoMorePaper.Application.Heartbeat;
using NoMorePaper.Worker.Telemetry;

namespace NoMorePaper.Worker.Heartbeat;

/// <summary>
/// Writes a heartbeat to Postgres at a fixed interval. A failed beat is logged and retried on the
/// next tick, so a database outage never stops the worker; the health check reports it instead.
/// </summary>
internal sealed class HeartbeatWriter(IServiceScopeFactory scopes, TimeProvider time, ILogger<HeartbeatWriter> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = (int)HeartbeatSchedule.Interval.TotalSeconds;
        logger.HeartbeatStarted(intervalSeconds);
        using var timer = new PeriodicTimer(HeartbeatSchedule.Interval, time);

        var failing = false;
        do
        {
            failing = await BeatAsync(failing, stoppingToken).ConfigureAwait(false);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    private async Task<bool> BeatAsync(bool wasFailing, CancellationToken stoppingToken)
    {
        using var activity = WorkerTelemetry.Source.StartActivity("nomorepaper.worker.heartbeat");
        try
        {
            var scope = scopes.CreateAsyncScope();
            await using (scope.ConfigureAwait(false))
            {
                var store = scope.ServiceProvider.GetRequiredService<IHeartbeatStore>();
                await store.RecordAsync(HeartbeatSchedule.WorkerName, time.GetUtcNow(), stoppingToken).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            WorkerTelemetry.HeartbeatFailures.Add(1);

            // Log the first failure of an outage only; the counter and /health show its duration.
            if (!wasFailing)
            {
                logger.HeartbeatFailed(exception);
            }

            return true;
        }

        if (wasFailing)
        {
            logger.HeartbeatRecovered();
        }

        return false;
    }
}
