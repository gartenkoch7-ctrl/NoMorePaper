using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Documents;
using NoMorePaper.Infrastructure.Persistence;
using NoMorePaper.Infrastructure.Processing;
using NoMorePaper.Worker.Processing;
using NoMorePaper.Worker.Tests.Persistence;
using Xunit;

namespace NoMorePaper.Worker.Tests.Processing;

/// <summary>
/// A <see cref="DocumentProcessor"/> wired like the Worker, on a migrated database and a data root of
/// its own, with a fake OCR engine and a clock the test sets.
/// </summary>
internal sealed class ProcessorHarness : IAsyncDisposable
{
    public const string FileName = "Scan 1.pdf";

    public static readonly DateTimeOffset Start = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

    private readonly PostgresFixture _postgres;
    private readonly string _database;
    private readonly ServiceProvider _services;
    private readonly DirectoryInfo _dataRoot = Directory.CreateTempSubdirectory("nomorepaper-worker-tests-");

    private ProcessorHarness(PostgresFixture postgres, string database)
    {
        _postgres = postgres;
        _database = database;
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(Time);
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(postgres.ConnectionString(database)));
        services.AddScoped<JobQueue>();
        _services = services.BuildServiceProvider();

        Storage = new FileSystemDocumentStorage(_dataRoot.FullName, new SkippedDirectorySync(), NullLogger<FileSystemDocumentStorage>.Instance);
        WorkingFolder = new WorkingFolder(Path.Combine(_dataRoot.FullName, WorkingFolder.FolderName), Time, NullLogger<WorkingFolder>.Instance);
        WorkingFolder.Prepare();
        Processor = new DocumentProcessor(
            _services.GetRequiredService<IServiceScopeFactory>(), new DocumentPipeline(Storage, Ocr), WorkingFolder, Time, Log);
    }

    public ManualTimeProvider Time { get; } = new(Start);

    public FakeOcrEngine Ocr { get; } = new();

    public RecordingLogger<DocumentProcessor> Log { get; } = new();

    public FileSystemDocumentStorage Storage { get; }

    public WorkingFolder WorkingFolder { get; }

    public DocumentProcessor Processor { get; }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task<ProcessorHarness> CreateAsync(PostgresFixture postgres, string database)
    {
        await using (var db = postgres.CreateContext(database))
        {
            await new DatabaseMigrator(db, TimeProvider.System, NullLogger<DatabaseMigrator>.Instance).MigrateAsync(Ct);
        }

        return new ProcessorHarness(postgres, database);
    }

    /// <summary>Receives a PDF like the upload does: original in the inbox, queued document, due job.</summary>
    /// <param name="attemptsSoFar">Attempts already counted on the job.</param>
    /// <returns>The id of the new document.</returns>
    public async Task<Guid> AddQueuedDocumentAsync(int attemptsSoFar = 0)
    {
        var document = Document.Receive("Scan 1", Start);
        using var content = new MemoryStream("%PDF-1.7 original"u8.ToArray());
        var stored = await Storage.SaveInboxAsync(document.Id, FileName, content, Ct);
        document.AttachFile(new DocumentFile(DocumentFileKind.Original, FileName, "application/pdf", stored.SizeBytes, stored.Sha256));

        await using var db = _postgres.CreateContext(_database);
        db.Documents.Add(document);
        db.ProcessingJobs.Add(new ProcessingJob { DocumentId = document.Id, Attempt = attemptsSoFar, NextAttemptAt = Start });
        await db.SaveChangesAsync(Ct);
        return document.Id;
    }

    /// <summary>A queue on a connection of its own, like another Worker's.</summary>
    public AppDbContext OtherWorkerContext() => _postgres.CreateContext(_database);

    public async Task<Document> DocumentAsync(Guid id)
    {
        await using var db = _postgres.CreateContext(_database);
        return await db.Documents.AsNoTracking().SingleAsync(document => document.Id == id, Ct);
    }

    public async Task<ProcessingJob?> JobAsync(Guid id)
    {
        await using var db = _postgres.CreateContext(_database);
        return await db.ProcessingJobs.AsNoTracking().SingleOrDefaultAsync(job => job.DocumentId == id, Ct);
    }

    /// <summary>What is left in the working folder; empty after every attempt.</summary>
    public string[] WorkingFolderEntries() => Directory.GetFileSystemEntries(WorkingFolder.Root);

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        _dataRoot.Delete(recursive: true);
    }
}
