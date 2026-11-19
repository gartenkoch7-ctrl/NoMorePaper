using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Domain.Documents;
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

    [Fact]
    public async Task Migrate_FromM1_KeepsDocumentsWithoutOwner_Shared_AndLoadable()
    {
        await using var db = postgres.CreateContext("migration_from_m1");
        await db.GetService<IMigrator>().MigrateAsync("20261118195546_ProcessingJobClaimId", TestContext.Current.CancellationToken);
        var id = Guid.CreateVersion7();
        await db.Database.ExecuteSqlAsync(
            $"INSERT INTO documents (id, title, received_at, state) VALUES ({id}, 'alt', now(), 'Queued')",
            TestContext.Current.CancellationToken);

        await Migrator(db).MigrateAsync(TestContext.Current.CancellationToken);

        // The Worker loads such documents to process them before the household is set up.
        var document = await db.Documents.AsNoTracking().SingleAsync(d => d.Id == id, TestContext.Current.CancellationToken);
        Assert.Equal((null, false), (document.OwnerId, document.IsPrivate));
    }

    [Fact]
    public async Task Migrate_FromM2_GivesExistingDocumentsEmptyFields_NotRecognizedYet()
    {
        await using var db = postgres.CreateContext("migration_from_m2");
        await db.GetService<IMigrator>().MigrateAsync("20261118224151_AdminPasswordResets", TestContext.Current.CancellationToken);
        var id = Guid.CreateVersion7();
        await db.Database.ExecuteSqlAsync(
            $"INSERT INTO documents (id, title, received_at, state, owner_id, is_private) VALUES ({id}, 'alt', now(), 'Done', {Guid.CreateVersion7()}, false)",
            TestContext.Current.CancellationToken);

        await Migrator(db).MigrateAsync(TestContext.Current.CancellationToken);

        var document = await db.Documents.AsNoTracking().SingleAsync(d => d.Id == id, TestContext.Current.CancellationToken);
        Assert.Equal((RecognitionState.None, null, null), (document.Recognition, document.Amount, document.CorrespondentId));
        Assert.Empty(document.Tags);
        Assert.Empty(document.Editors);
    }

    [Fact]
    public async Task Migrate_MovesAnOpenAdminResetNotice_IntoTheAppendOnlyTable()
    {
        await using var db = postgres.CreateContext("migration_admin_resets");
        await db.GetService<IMigrator>().MigrateAsync("20261118220559_AdminResetNotice", TestContext.Current.CancellationToken);
        var told = Guid.CreateVersion7();
        var untold = Guid.CreateVersion7();
        var resetAt = new DateTimeOffset(2026, 11, 18, 21, 0, 0, TimeSpan.Zero);
        foreach (var (id, at) in new[] { (told, (DateTimeOffset?)resetAt), (untold, null) })
        {
            await db.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO users (id, display_name, is_admin, email_confirmed, phone_number_confirmed, two_factor_enabled,
                                   lockout_enabled, access_failed_count, password_reset_by_admin_at)
                VALUES ({id}, 'x', false, false, false, false, true, 0, {at})
                """,
                TestContext.Current.CancellationToken);
        }

        await Migrator(db).MigrateAsync(TestContext.Current.CancellationToken);

        var resets = await db.AdminPasswordResets.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);
        var moved = Assert.Single(resets);
        Assert.Equal((told, resetAt, (DateTimeOffset?)null), (moved.UserId, moved.ResetAt, moved.AcknowledgedAt));
    }

    private static DatabaseMigrator Migrator(AppDbContext db) =>
        new(db, TimeProvider.System, NullLogger<DatabaseMigrator>.Instance);
}
