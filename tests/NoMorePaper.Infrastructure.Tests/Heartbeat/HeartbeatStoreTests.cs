using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Infrastructure.Heartbeat;
using NoMorePaper.Infrastructure.Persistence;
using NoMorePaper.Infrastructure.Tests.Persistence;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Heartbeat;

[Collection(SharedPostgres.Name)]
public sealed class HeartbeatStoreTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset s_firstBeat = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetLastBeat_IsNull_BeforeTheFirstBeat()
    {
        await using var db = await MigratedContextAsync("heartbeat_empty");

        Assert.Null(await new HeartbeatStore(db).GetLastBeatAsync("worker", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Record_StoresTheBeat_AndLaterBeatsReplaceIt()
    {
        await using var db = await MigratedContextAsync("heartbeat_upsert");
        var store = new HeartbeatStore(db);
        var ct = TestContext.Current.CancellationToken;

        await store.RecordAsync("worker", s_firstBeat, ct);
        Assert.Equal(s_firstBeat, await store.GetLastBeatAsync("worker", ct));

        var secondBeat = s_firstBeat.AddSeconds(15);
        await store.RecordAsync("worker", secondBeat, ct);
        Assert.Equal(secondBeat, await store.GetLastBeatAsync("worker", ct));
        Assert.Equal(1, db.WorkerHeartbeats.Count());
    }

    [Fact]
    public async Task Record_StoresLocalOffsetsAsTheSameInstant()
    {
        await using var db = await MigratedContextAsync("heartbeat_offset");
        var store = new HeartbeatStore(db);
        var ct = TestContext.Current.CancellationToken;

        await store.RecordAsync("worker", s_firstBeat.ToOffset(TimeSpan.FromHours(1)), ct);

        Assert.Equal(s_firstBeat, await store.GetLastBeatAsync("worker", ct));
    }

    private async Task<AppDbContext> MigratedContextAsync(string database)
    {
        var db = postgres.CreateContext(database);
        await new DatabaseMigrator(db, TimeProvider.System, NullLogger<DatabaseMigrator>.Instance)
            .MigrateAsync(TestContext.Current.CancellationToken);
        return db;
    }
}
