using System.Diagnostics;
using NoMorePaper.Infrastructure.Persistence;
using NoMorePaper.Server.Telemetry;

namespace NoMorePaper.Server.Startup;

/// <summary>
/// Applies pending EF Core migrations while the host starts. Only the Server migrates; the Worker
/// waits for a healthy Server (docker-compose.yml). A failure stops the host instead of serving
/// requests against an outdated schema.
/// </summary>
internal sealed class MigrateOnStartup(IServiceScopeFactory scopes, TimeProvider time, ILogger<MigrateOnStartup> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var activity = ServerTelemetry.Source.StartActivity("nomorepaper.server.startup");
        var started = time.GetTimestamp();

        try
        {
            var scope = scopes.CreateAsyncScope();
            await using (scope.ConfigureAwait(false))
            {
                var migrator = scope.ServiceProvider.GetRequiredService<DatabaseMigrator>();
                await migrator.MigrateAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            logger.StartupFailed(exception);
            throw;
        }

        var elapsedMs = (long)time.GetElapsedTime(started).TotalMilliseconds;
        logger.StartupCompleted(elapsedMs);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
