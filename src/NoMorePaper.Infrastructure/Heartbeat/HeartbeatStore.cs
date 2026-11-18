using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Heartbeat;
using NoMorePaper.Infrastructure.Persistence;

namespace NoMorePaper.Infrastructure.Heartbeat;

/// <summary>Reads and writes worker heartbeats in Postgres.</summary>
public sealed class HeartbeatStore(AppDbContext db) : IHeartbeatStore
{
    public async Task RecordAsync(string workerName, DateTimeOffset beatAt, CancellationToken cancellationToken)
    {
        // One atomic upsert instead of read-then-write; the values are sent as parameters.
        var utc = beatAt.ToUniversalTime();
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO worker_heartbeats (worker_name, last_beat_at) VALUES ({workerName}, {utc})
            ON CONFLICT (worker_name) DO UPDATE SET last_beat_at = EXCLUDED.last_beat_at
            """,
            cancellationToken).ConfigureAwait(false);
    }

    public Task<DateTimeOffset?> GetLastBeatAsync(string workerName, CancellationToken cancellationToken) =>
        db.WorkerHeartbeats
            .AsNoTracking()
            .Where(heartbeat => heartbeat.WorkerName == workerName)
            .Select(heartbeat => (DateTimeOffset?)heartbeat.LastBeatAt)
            .SingleOrDefaultAsync(cancellationToken);
}
