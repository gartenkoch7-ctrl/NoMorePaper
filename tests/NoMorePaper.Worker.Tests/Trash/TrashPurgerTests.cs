using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Application.Documents;
using NoMorePaper.Application.Editing;
using NoMorePaper.Application.Trash;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Documents;
using NoMorePaper.Infrastructure.Editing;
using NoMorePaper.Infrastructure.Persistence;
using NoMorePaper.Infrastructure.Trash;
using NoMorePaper.Worker.Tests.Persistence;
using NoMorePaper.Worker.Tests.Processing;
using NoMorePaper.Worker.Trash;
using Xunit;

namespace NoMorePaper.Worker.Tests.Trash;

/// <summary>
/// The daily purge with a clock the test sets (ADR-0025): an entry stays 30 days, then its rows and files go
/// for good; a failed run never stops the Worker host.
/// </summary>
[Collection(SharedPostgres.Name)]
public sealed class TrashPurgerTests(PostgresFixture postgres) : IDisposable
{
    private static readonly DateTimeOffset s_trashedAt = new(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);

    private readonly DirectoryInfo _dataRoot = Directory.CreateTempSubdirectory("nomorepaper-purger-tests-");
    private readonly ManualTimeProvider _time = new(s_trashedAt);
    private readonly RecordingLogger<TrashPurger> _log = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _dataRoot.Delete(recursive: true);

    [Fact]
    public async Task AnEntry_StaysThirtyDays_ThenItsRowsAndFilesGoForGood()
    {
        const string database = "trash_purger";
        await using (var db = postgres.CreateContext(database))
        {
            await new DatabaseMigrator(db, TimeProvider.System, NullLogger<DatabaseMigrator>.Instance).MigrateAsync(Ct);
        }

        await using var services = Services(database, withPurge: true);
        var storage = services.GetRequiredService<IDocumentStorage>();
        var (trashed, edited) = await SeedAsync(postgres.CreateContext(database), storage);
        var purger = new TrashPurger(services.GetRequiredService<IServiceScopeFactory>(), _time, _log);

        _time.Now = s_trashedAt.AddDays(30).AddMinutes(-1);
        Assert.Equal(0, await purger.RunOnceAsync(Ct));
        Assert.True(await ExistsAsync(database, trashed.Id));
        Assert.True(File.Exists(Path.Combine(TrashFolder(edited), "edited.pdf")));

        _time.Now = s_trashedAt.AddDays(30).AddMinutes(1);
        Assert.Equal(1, await purger.RunOnceAsync(Ct));
        Assert.False(await ExistsAsync(database, trashed.Id));
        Assert.Throws<FileNotFoundException>(() => storage.OpenRead(trashed.Id, DocumentFileKind.Original));
        Assert.True(Directory.Exists(TrashFolder(edited)));

        _time.Now = s_trashedAt.AddDays(40).AddMinutes(1);
        Assert.Equal(1, await purger.RunOnceAsync(Ct));
        Assert.False(Directory.Exists(TrashFolder(edited)));
        Assert.True(await ExistsAsync(database, edited.DocumentId));
        Assert.Equal([2501, 2501, 2501], _log.EventIds);
    }

    [Fact]
    public async Task AFailedRun_IsLogged_AndDoesNotStopTheHost()
    {
        await using var services = Services("trash_purger_broken", withPurge: false);
        using var purger = new TrashPurger(services.GetRequiredService<IServiceScopeFactory>(), _time, _log);

        Assert.Null(await purger.RunOnceAsync(Ct));

        await purger.StartAsync(Ct);

        // Since .NET 10 ExecuteAsync runs on the thread pool, so stopping right away could cancel the first run.
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (_log.Entries.Count < 3 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10, Ct);
        }

        await purger.StopAsync(Ct);
        Assert.False(purger.ExecuteTask!.IsFaulted);
        Assert.Equal([2502, 2500, 2502], _log.EventIds);
    }

    // A document of Anna's in the trash since the start, and an edited version of Bob's 10 days later.
    private static async Task<(Document Trashed, TrashItem Edited)> SeedAsync(AppDbContext db, IDocumentStorage storage)
    {
        await using (db)
        {
            var trashed = await DoneAsync(db, storage, Guid.CreateVersion7());
            trashed.MoveToTrash(trashed.OwnerId!.Value, s_trashedAt);
            await new EfTrashStore(db).AddAsync(TrashItem.ForDocument(trashed, s_trashedAt), Ct);

            var kept = await DoneAsync(db, storage, Guid.CreateVersion7());
            var stored = await storage.SaveEditedAsync(kept.Id, new MemoryStream("%PDF-1.7 edited"u8.ToArray()), Ct);
            var file = new DocumentFile(DocumentFileKind.Edited, "edited.pdf", "application/pdf", stored.SizeBytes, stored.Sha256);
            var edited = TrashItem.ForFile(kept, file, s_trashedAt.AddDays(10));
            await new EfTrashStore(db).AddAsync(edited, Ct);
            await storage.MoveToTrashAsync(kept.Id, DocumentFileKind.Edited, edited.Id, Ct);
            return (trashed, edited);
        }
    }

    private static async Task<Document> DoneAsync(AppDbContext db, IDocumentStorage storage, Guid owner)
    {
        var document = Document.Receive("Brief", s_trashedAt.AddDays(-1), owner, isPrivate: false);
        var stored = await storage.SaveInboxAsync(document.Id, "scan.pdf", new MemoryStream("%PDF-1.7 original"u8.ToArray()), Ct);
        await storage.ArchiveOriginalAsync(document.Id, Ct);
        document.AttachFile(new DocumentFile(DocumentFileKind.Original, "scan.pdf", "application/pdf", stored.SizeBytes, stored.Sha256));
        document.StartProcessing();
        document.Complete("Rechnung");
        db.Documents.Add(document);
        await db.SaveChangesAsync(Ct);
        return document;
    }

    private async Task<bool> ExistsAsync(string database, Guid id)
    {
        await using var db = postgres.CreateContext(database);
        return await db.Documents.IgnoreQueryFilters().AnyAsync(document => document.Id == id, Ct);
    }

    private string TrashFolder(TrashItem item) => Path.Combine(_dataRoot.FullName, "trash", item.Id.ToString());

    private ServiceProvider Services(string database, bool withPurge)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(_time);
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(postgres.ConnectionString(database)));
        services.AddScoped<ITrashStore, EfTrashStore>();
        services.AddScoped<IDocumentEdits, EfDocumentEdits>();
        services.AddSingleton<IDocumentStorage>(
            new FileSystemDocumentStorage(_dataRoot.FullName, new SkippedDirectorySync(), NullLogger<FileSystemDocumentStorage>.Instance));
        if (withPurge)
        {
            services.AddTrashPurge();
        }

        return services.BuildServiceProvider();
    }
}
