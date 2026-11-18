using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Application.Documents;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Documents;
using NoMorePaper.Infrastructure.Persistence;
using NoMorePaper.Infrastructure.Tests.Persistence;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Documents;

[Collection(SharedPostgres.Name)]
public sealed class ReceiveDocumentTests(PostgresFixture postgres) : IDisposable
{
    private static readonly byte[] s_content = [.. Enumerable.Range(0, 50_000).Select(i => (byte)(i % 241))];

    private readonly DirectoryInfo _sandbox = Directory.CreateTempSubdirectory("nomorepaper-intake-");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _sandbox.Delete(recursive: true);

    [Fact]
    public async Task Receive_StoresTheFile_AndRecordsDocumentAndJobTogether()
    {
        await using var db = await MigratedContextAsync("intake_ok");
        var intake = CreateIntake(db);

        var id = await intake.ReceiveAsync("Zahnarzt 2026.PDF", "application/pdf", new MemoryStream(s_content), Ct);

        await using var verify = postgres.CreateContext("intake_ok");
        var document = await verify.Documents.SingleAsync(d => d.Id == id, Ct);
        Assert.Equal("Zahnarzt 2026", document.Title);
        Assert.Equal(ProcessingState.Queued, document.State);
        Assert.Equal(7, document.Id.Version);
        var original = Assert.Single(document.Files);
        Assert.Equal(
            (DocumentFileKind.Original, "Zahnarzt 2026.PDF", "application/pdf", (long)s_content.Length, Convert.ToHexStringLower(SHA256.HashData(s_content))),
            (original.Kind, original.FileName, original.MediaType, original.SizeBytes, original.Sha256));

        var job = await verify.ProcessingJobs.SingleAsync(j => j.DocumentId == id, Ct);
        Assert.Equal(0, job.Attempt);
        Assert.Null(job.LockedUntil);
        Assert.True(job.NextAttemptAt <= DateTimeOffset.UtcNow);

        Assert.Equal(s_content, await File.ReadAllBytesAsync(Path.Combine(_sandbox.FullName, "inbox", id.ToString(), "original.pdf"), Ct));
    }

    [Fact]
    public async Task Receive_UsesTheClock_ForReceivedAtAndTheJobDueTime()
    {
        await using var db = await MigratedContextAsync("intake_clock");
        var now = new DateTimeOffset(2026, 5, 6, 7, 8, 9, TimeSpan.Zero);
        var intake = CreateIntake(db, new FixedClock(now));

        var id = await intake.ReceiveAsync("a.png", "image/png", new MemoryStream(s_content), Ct);

        await using var verify = postgres.CreateContext("intake_clock");
        Assert.Equal(now, (await verify.Documents.SingleAsync(d => d.Id == id, Ct)).ReceivedAt);
        Assert.Equal(now, (await verify.ProcessingJobs.SingleAsync(j => j.DocumentId == id, Ct)).NextAttemptAt);
    }

    [Fact]
    public async Task Receive_NeverUsesDirectoriesOfTheReceivedName()
    {
        await using var db = await MigratedContextAsync("intake_path");

        var id = await CreateIntake(db).ReceiveAsync("../../outside/secret.pdf", "application/pdf", new MemoryStream(s_content), Ct);

        Assert.True(File.Exists(Path.Combine(_sandbox.FullName, "inbox", id.ToString(), "original.pdf")));
        Assert.Equal("secret.pdf", (await db.Documents.AsNoTracking().SingleAsync(d => d.Id == id, Ct)).Files.Single().FileName);
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(_sandbox.FullName)!, "secret*"));
    }

    [Fact]
    public async Task Receive_WhenTheDatabaseRefuses_RemovesTheStoredFile_AndRethrows()
    {
        await using var db = await MigratedContextAsync("intake_refused");
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE FUNCTION refuse() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'refused'; END $$;
            CREATE TRIGGER refuse BEFORE INSERT ON processing_jobs FOR EACH ROW EXECUTE FUNCTION refuse();
            """,
            Ct);

        await Assert.ThrowsAnyAsync<DbUpdateException>(() =>
            CreateIntake(db).ReceiveAsync("a.pdf", "application/pdf", new MemoryStream(s_content), Ct));

        Assert.Empty(await db.Documents.AsNoTracking().ToListAsync(Ct));
        Assert.Empty(InboxFolders());
    }

    [Fact]
    public async Task Receive_WhenTheCommitReportsAFailureAfterItWasApplied_KeepsTheFile()
    {
        await using var db = await MigratedContextAsync("intake_applied", new FailAfterSave());

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            CreateIntake(db).ReceiveAsync("a.pdf", "application/pdf", new MemoryStream(s_content), Ct));

        var document = await db.Documents.AsNoTracking().SingleAsync(Ct);
        Assert.Equal(s_content, await File.ReadAllBytesAsync(Path.Combine(InboxFolders().Single(), "original.pdf"), Ct));
        Assert.EndsWith(document.Id.ToString(), InboxFolders().Single(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Receive_WhenTheRecordCannotBeChecked_KeepsTheFile()
    {
        // This database was never created, so both saving and the check afterwards fail.
        await using var db = postgres.CreateContext("intake_unmigrated");

        await Assert.ThrowsAnyAsync<Exception>(() =>
            CreateIntake(db).ReceiveAsync("a.pdf", "application/pdf", new MemoryStream(s_content), Ct));

        Assert.Single(InboxFolders());
    }

    [Fact]
    public async Task Receive_WhenTheRequestIsCancelledDuringTheCommit_StillCommits()
    {
        using var request = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        await using var db = await MigratedContextAsync("intake_cancel_commit", new CancelBeforeSave(request));

        var id = await CreateIntake(db).ReceiveAsync("a.pdf", "application/pdf", new MemoryStream(s_content), request.Token);

        Assert.True(request.IsCancellationRequested);
        Assert.True(await db.ProcessingJobs.AsNoTracking().AnyAsync(j => j.DocumentId == id, Ct));
        Assert.Single(InboxFolders());
    }

    [Fact]
    public async Task Receive_WhenReadingTheContentFails_LeavesNoDocumentBehind()
    {
        await using var db = await MigratedContextAsync("intake_failing_stream");

        await Assert.ThrowsAsync<IOException>(() =>
            CreateIntake(db).ReceiveAsync("a.pdf", "application/pdf", new FailingStream(s_content, 10_000), Ct));

        Assert.Empty(await db.Documents.ToListAsync(Ct));
        Assert.Empty(await db.ProcessingJobs.ToListAsync(Ct));
    }

    private ReceiveDocument CreateIntake(AppDbContext db, TimeProvider? clock = null) => new(
        new EfDocumentRepository(db),
        new FileSystemDocumentStorage(_sandbox.FullName, new RecordingDirectorySync(), NullLogger<FileSystemDocumentStorage>.Instance),
        clock ?? TimeProvider.System,
        NullLogger<ReceiveDocument>.Instance);

    private string[] InboxFolders()
    {
        var inbox = Path.Combine(_sandbox.FullName, "inbox");
        return Directory.Exists(inbox) ? Directory.GetDirectories(inbox) : [];
    }

    private async Task<AppDbContext> MigratedContextAsync(string database, params IInterceptor[] interceptors)
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(postgres.ConnectionString(database))
            .AddInterceptors(interceptors)
            .Options);
        await new DatabaseMigrator(db, TimeProvider.System, NullLogger<DatabaseMigrator>.Instance).MigrateAsync(Ct);
        return db;
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>The rows are committed, but the caller hears of a cancellation, like a lost answer.</summary>
    private sealed class FailAfterSave : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) =>
            throw new OperationCanceledException();
    }

    /// <summary>The upload is aborted just as the commit starts.</summary>
    private sealed class CancelBeforeSave(CancellationTokenSource request) : SaveChangesInterceptor
    {
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            await request.CancelAsync();
            return result;
        }
    }
}
