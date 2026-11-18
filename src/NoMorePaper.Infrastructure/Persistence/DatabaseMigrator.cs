using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoMorePaper.Infrastructure.Telemetry;

namespace NoMorePaper.Infrastructure.Persistence;

/// <summary>Brings the database schema up to date. Called once by the Server at startup.</summary>
public sealed class DatabaseMigrator(AppDbContext db, TimeProvider time, ILogger<DatabaseMigrator> logger)
{
    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        using var activity = InfrastructureTelemetry.Source.StartActivity("nomorepaper.database.migrate");
        var started = time.GetTimestamp();
        var knownMigrations = db.Database.GetMigrations().Count();
        logger.MigrationStarting(knownMigrations);

        try
        {
            // EF Core holds a database lock while migrating, so concurrent starts cannot migrate twice.
            await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            logger.MigrationFailed(exception);
            throw;
        }

        var elapsedMs = (long)time.GetElapsedTime(started).TotalMilliseconds;
        logger.MigrationCompleted(elapsedMs);
    }
}
