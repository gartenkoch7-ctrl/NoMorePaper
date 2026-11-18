using Microsoft.Extensions.Logging;

namespace NoMorePaper.Infrastructure.Persistence;

/// <summary>Log events of the persistence feature (Infrastructure range 3000–3999, docs/kb/telemetrie.md).</summary>
internal static partial class PersistenceLog
{
    [LoggerMessage(EventId = 3000, Level = LogLevel.Information, Message = "Applying database migrations ({KnownMigrations} known)")]
    public static partial void MigrationStarting(this ILogger logger, int knownMigrations);

    [LoggerMessage(EventId = 3001, Level = LogLevel.Information, Message = "Database schema is up to date after {ElapsedMs} ms")]
    public static partial void MigrationCompleted(this ILogger logger, long elapsedMs);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Error, Message = "Database migration failed")]
    public static partial void MigrationFailed(this ILogger logger, Exception exception);
}
