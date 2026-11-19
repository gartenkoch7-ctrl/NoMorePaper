using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Application.Processing;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Persistence;
using NoMorePaper.Infrastructure.Processing;
using NoMorePaper.Infrastructure.Tests.Persistence;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Processing;

/// <summary>A migrated database of its own per test, filled like the upload does: queued document, original, job.</summary>
internal sealed class QueueDatabase(PostgresFixture postgres, string database)
{
    public const string Sha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    public static readonly DateTimeOffset Start = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid s_owner = Guid.CreateVersion7();

    public static readonly TimeSpan LockFor = TimeSpan.FromMinutes(15);

    public ManualTimeProvider Time { get; } = new(Start);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task<QueueDatabase> CreateAsync(PostgresFixture postgres, string database)
    {
        await using var db = postgres.CreateContext(database);
        await new DatabaseMigrator(db, TimeProvider.System, NullLogger<DatabaseMigrator>.Instance).MigrateAsync(Ct);
        return new QueueDatabase(postgres, database);
    }

    public AppDbContext Context() => postgres.CreateContext(database);

    public JobQueue Queue(AppDbContext db) => new(db, Time);

    /// <summary>Owner of the documents added here.</summary>
    public static Guid Owner => s_owner;

    /// <returns>The id of the new document.</returns>
    public async Task<Guid> AddQueuedDocumentAsync(DateTimeOffset nextAttemptAt, string fileName = "Scan 1.jpg", int priority = 0)
    {
        await using var db = Context();
        var document = Document.Receive("Scan 1", Start, s_owner, isPrivate: false);
        document.AttachFile(new DocumentFile(DocumentFileKind.Original, fileName, "image/jpeg", 10, Sha256));
        db.Documents.Add(document);
        db.ProcessingJobs.Add(new ProcessingJob { DocumentId = document.Id, NextAttemptAt = nextAttemptAt, Priority = priority });
        await db.SaveChangesAsync(Ct);
        return document.Id;
    }

    /// <summary>A done document with a job at the recognition stage, as OCR leaves it.</summary>
    /// <returns>The id of the new document.</returns>
    public async Task<Guid> AddRecognitionJobAsync(DateTimeOffset nextAttemptAt, int priority = 0)
    {
        await using var db = Context();
        var document = Document.Receive("Scan 1", Start, s_owner, isPrivate: false);
        document.AttachFile(new DocumentFile(DocumentFileKind.Original, "Scan 1.jpg", "image/jpeg", 10, Sha256));
        document.StartProcessing();
        document.Complete("Rechnung");
        db.Documents.Add(document);
        db.ProcessingJobs.Add(new ProcessingJob
        {
            DocumentId = document.Id, Stage = JobStage.Recognition, NextAttemptAt = nextAttemptAt, Priority = priority,
        });
        await db.SaveChangesAsync(Ct);
        return document.Id;
    }

    public async Task<Document> DocumentAsync(Guid id)
    {
        await using var db = Context();
        return await db.Documents.AsNoTracking().SingleAsync(document => document.Id == id, Ct);
    }

    public async Task<ProcessingJob?> JobAsync(Guid id)
    {
        await using var db = Context();
        return await db.ProcessingJobs.AsNoTracking().SingleOrDefaultAsync(job => job.DocumentId == id, Ct);
    }
}
