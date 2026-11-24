using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Application.Classification;
using NoMorePaper.Application.Composition;
using NoMorePaper.Application.Documents;
using NoMorePaper.Application.Editing;
using NoMorePaper.Application.Processing;
using NoMorePaper.Application.Recognition;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Classification;
using NoMorePaper.Infrastructure.Documents;
using NoMorePaper.Infrastructure.Persistence;
using NoMorePaper.Infrastructure.Processing;
using NoMorePaper.Infrastructure.Recognition;
using NoMorePaper.Worker.Processing;
using NoMorePaper.Worker.Tests.Editing;
using NoMorePaper.Worker.Tests.Persistence;
using Xunit;

namespace NoMorePaper.Worker.Tests.Processing;

/// <summary>
/// A <see cref="ProcessNextDocument"/> wired like the Worker, on a migrated database and a data root of
/// its own, with a fake OCR engine and a clock the test sets.
/// </summary>
internal sealed class ProcessorHarness : IAsyncDisposable
{
    public const string FileName = "Scan 1.pdf";

    public static readonly DateTimeOffset Start = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid s_owner = Guid.CreateVersion7();

    private readonly PostgresFixture _postgres;
    private readonly string _database;
    private readonly ServiceProvider _services;
    private readonly DirectoryInfo _dataRoot = Directory.CreateTempSubdirectory("nomorepaper-worker-tests-");

    private ProcessorHarness(PostgresFixture postgres, string database)
    {
        _postgres = postgres;
        _database = database;
        Storage = new FileSystemDocumentStorage(_dataRoot.FullName, new SkippedDirectorySync(), NullLogger<FileSystemDocumentStorage>.Instance);
        WorkingFolder = new WorkingFolder(Path.Combine(_dataRoot.FullName, WorkingFolder.FolderName), Time, NullLogger<WorkingFolder>.Instance);
        WorkingFolder.Prepare();

        var services = new ServiceCollection();
        services.AddSingleton<IDocumentStorage>(Storage);
        services.AddSingleton<IOcrEngine>(Ocr);
        services.AddSingleton<IWorkingFolder>(WorkingFolder);
        services.AddSingleton<IPageAnalyzer>(Pages);
        services.AddSingleton<ILogger<ProcessNextDocument>>(Log);
        services.AddStageRunners();
        services.AddSingleton<TimeProvider>(Time);
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(postgres.ConnectionString(database)));
        services.AddScoped<IJobQueue, JobQueue>();
        services.AddScoped<IClassificationStore, ClassificationStore>();
        services.AddScoped<IRecognitionExamples, EfRecognitionExamples>();
        services.AddScoped<IDuplicateFinder, PostgresDuplicateFinder>();
        services.AddSingleton<IRecognizer>(Recognizer);
        services.AddSingleton<ILogger<RecognizeDocument>>(RecognitionLog);
        services.AddSingleton<ILogger<FindContentDuplicate>>(DuplicateLog);
        services.AddSingleton<ILogger<EnqueueBacklog>>(NullLogger<EnqueueBacklog>.Instance);
        services.AddScoped<RecognizeDocument>();
        services.AddScoped<FindContentDuplicate>();
        services.AddScoped<EnqueueBacklog>();
        services.AddSeparation(Separator);
        EditingServices.Add(services, _dataRoot.FullName, Pdf);
        _services = services.BuildServiceProvider();

        Readiness.MarkReady();
        Processor = new ProcessNextDocument(_services.GetRequiredService<IServiceScopeFactory>(), Readiness, Time, Log);
    }

    /// <summary>Stands in for qpdf in the edit stage.</summary>
    public FakePdfTool Pdf { get; } = new();

    /// <summary>The data root of documents, edits and the trash.</summary>
    public string DataRoot => _dataRoot.FullName;

    /// <summary>Stands in for the page analysis with poppler.</summary>
    public FakePageAnalyzer Pages { get; } = new();

    /// <summary>Ready from the start; a test that waits for the model sets up its own.</summary>
    public RecognitionReadiness Readiness { get; } = new();

    public ManualTimeProvider Time { get; } = new(Start);

    public FakeOcrEngine Ocr { get; } = new();

    public RecordingLogger<ProcessNextDocument> Log { get; } = new();

    public FakeRecognizer Recognizer { get; } = new();

    /// <summary>Stands in for the local LLM of the stack separation.</summary>
    public FakeSeparationAssessor Separator { get; } = new();

    public RecordingLogger<RecognizeDocument> RecognitionLog { get; } = new();

    public RecordingLogger<FindContentDuplicate> DuplicateLog { get; } = new();

    /// <summary>Owner of the documents <see cref="AddQueuedDocumentAsync"/> adds.</summary>
    public static Guid Owner => s_owner;

    public FileSystemDocumentStorage Storage { get; }

    public WorkingFolder WorkingFolder { get; }

    public ProcessNextDocument Processor { get; }

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
    /// <param name="isPrivate">Whether the document is private to its owner.</param>
    /// <returns>The id of the new document.</returns>
    public async Task<Guid> AddQueuedDocumentAsync(int attemptsSoFar = 0, bool isPrivate = false)
    {
        var document = Document.Receive("Scan 1", Start, s_owner, isPrivate);
        using var content = new MemoryStream("%PDF-1.7 original"u8.ToArray());
        var stored = await Storage.SaveInboxAsync(document.Id, FileName, content, Ct);
        document.AttachFile(new DocumentFile(DocumentFileKind.Original, FileName, "application/pdf", stored.SizeBytes, stored.Sha256));

        await using var db = _postgres.CreateContext(_database);
        db.Documents.Add(document);
        db.ProcessingJobs.Add(new ProcessingJob { DocumentId = document.Id, Attempt = attemptsSoFar, NextAttemptAt = Start });
        await db.SaveChangesAsync(Ct);
        return document.Id;
    }

    /// <summary>Scopes with the Worker's processing services on the test database.</summary>
    public IServiceScopeFactory Scopes => _services.GetRequiredService<IServiceScopeFactory>();

    /// <summary>A queue on a connection of its own, like another Worker's.</summary>
    public AppDbContext OtherWorkerContext() => _postgres.CreateContext(_database);

    /// <summary>A processed document without job, e.g. of another account or from before recognition.</summary>
    /// <param name="classify">Sets fields on the document before it is stored, given the context for entries.</param>
    public async Task<Document> AddDoneDocumentAsync(
        Guid owner, bool isPrivate, string text, Func<AppDbContext, Document, Task>? classify = null)
    {
        await using var db = _postgres.CreateContext(_database);
        var document = Document.Receive("Brief", Start.AddDays(-1), owner, isPrivate);
        document.StartProcessing();
        document.Complete(text);
        if (classify is not null)
        {
            await classify(db, document);
        }

        db.Documents.Add(document);
        await db.SaveChangesAsync(Ct);
        return document;
    }

    /// <summary>Processes jobs until none is due.</summary>
    /// <returns>How many jobs were processed.</returns>
    public async Task<int> ProcessAllAsync()
    {
        var count = 0;
        while (await Processor.ProcessNextAsync(Ct))
        {
            count++;
        }

        return count;
    }

    /// <summary>Runs the backlog use case like the Worker does at its start.</summary>
    public async Task<int> EnqueueBacklogAsync()
    {
        await using var scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<EnqueueBacklog>().RunAsync(Ct);
    }

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
