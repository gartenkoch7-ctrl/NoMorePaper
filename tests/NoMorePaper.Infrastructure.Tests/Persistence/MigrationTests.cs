using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Infrastructure.Persistence;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Persistence;

[Collection(SharedPostgres.Name)]
public sealed class MigrationTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Migrate_CreatesTheDatabase_AndAppliesEveryMigration()
    {
        await using var db = postgres.CreateContext("migration_fresh");

        await Migrator(db).MigrateAsync(TestContext.Current.CancellationToken);

        var applied = await db.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken);
        var pending = await db.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken);
        Assert.Equal(db.Database.GetMigrations(), applied);
        Assert.Empty(pending);
        Assert.True(await db.Database.CanConnectAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Migrate_IsIdempotent()
    {
        await using var db = postgres.CreateContext("migration_twice");

        await Migrator(db).MigrateAsync(TestContext.Current.CancellationToken);
        await Migrator(db).MigrateAsync(TestContext.Current.CancellationToken);

        Assert.Empty(await db.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Model_HasNoChangesMissingFromTheMigrations()
    {
        await using var db = postgres.CreateContext("migration_model");

        // Fails when an entity changed without `dotnet ef migrations add`.
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Migrate_CreatesTheTrigramExtension_AndTheSearchIndexes()
    {
        await using var db = postgres.CreateContext("migration_search");
        await Migrator(db).MigrateAsync(TestContext.Current.CancellationToken);

        var extensions = await db.Database
            .SqlQuery<string>($"SELECT extname::text AS \"Value\" FROM pg_extension")
            .ToListAsync(TestContext.Current.CancellationToken);
        var indexes = await db.Database
            .SqlQuery<string>($"SELECT indexdef AS \"Value\" FROM pg_indexes WHERE tablename = 'documents'")
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Contains("pg_trgm", extensions);
        Assert.Contains(indexes, definition => definition.Contains("USING gin (search_vector)", StringComparison.Ordinal));
        Assert.Contains(indexes, definition => definition.Contains("USING gin (content_text gin_trgm_ops)", StringComparison.Ordinal));
    }

    private static DatabaseMigrator Migrator(AppDbContext db) =>
        new(db, TimeProvider.System, NullLogger<DatabaseMigrator>.Instance);
}
