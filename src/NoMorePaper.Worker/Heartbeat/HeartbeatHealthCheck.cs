using Microsoft.Extensions.Diagnostics.HealthChecks;
using NoMorePaper.Application.Heartbeat;

namespace NoMorePaper.Worker.Heartbeat;

/// <summary>Healthy while the last heartbeat in the database is recent: proves both loop and database.</summary>
internal sealed class HeartbeatHealthCheck(IHeartbeatStore store, TimeProvider time) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var lastBeat = await store.GetLastBeatAsync(HeartbeatSchedule.WorkerName, cancellationToken).ConfigureAwait(false);
        if (lastBeat is null)
        {
            return HealthCheckResult.Unhealthy("No heartbeat recorded yet");
        }

        var age = time.GetUtcNow() - lastBeat.Value;
        return age <= HeartbeatSchedule.StaleAfter
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Heartbeat is stale");
    }
}
