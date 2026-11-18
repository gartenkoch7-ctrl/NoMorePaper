using Microsoft.Extensions.Diagnostics.HealthChecks;
using NoMorePaper.Application.Persistence;

namespace NoMorePaper.Server.Health;

/// <summary>The Server is only healthy while it can reach its database.</summary>
internal sealed class DatabaseHealthCheck(IDatabaseProbe database) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await database.CanConnectAsync(cancellationToken).ConfigureAwait(false)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Database not reachable");
}
