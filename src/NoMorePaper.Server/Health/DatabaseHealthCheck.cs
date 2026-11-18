using Microsoft.Extensions.Diagnostics.HealthChecks;
using NoMorePaper.Infrastructure.Persistence;

namespace NoMorePaper.Server.Health;

/// <summary>The Server is only healthy while it can reach its database.</summary>
internal sealed class DatabaseHealthCheck(AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await db.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Database not reachable");
}
