using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Persistence;
using NoMorePaper.Infrastructure.Processing;
using NoMorePaper.Infrastructure.Tests.Persistence;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Documents;

[Collection(SharedPostgres.Name)]
public sealed class DocumentSchemaTests(PostgresFixture postgres)
{
    private const string Sha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
    private static readonly DateTimeOffset s_received = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid s_owner = Guid.CreateVersion7();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Document_RoundTripsWithItsFilesAndJob()
    {
        const string database = "documents_roundtrip";
        var document = Document.Receive("Brief", s_received, s_owner, isPrivate: false);
        document.AttachFile(new DocumentFile(DocumentFileKind.Original, "Scan 1.jpg", "image/jpeg", 1234, Sha256));
        await using (var db = await MigratedContextAsync(database))
        {
            db.Documents.Add(document);
            db.ProcessingJobs.Add(new ProcessingJob { DocumentId = document.Id, NextAttemptAt = s_received });
            await db.SaveChangesAsync(Ct);
        }

        await using (var db = postgres.CreateContext(database))
        {
            var loaded = await db.Documents.SingleAsync(d => d.Id == document.Id, Ct);
            Assert.Equal("Brief", loaded.Title);
            Assert.Equal(s_received, loaded.ReceivedAt);
            Assert.Equal(ProcessingState.Queued, loaded.State);
            var original = Assert.Single(loaded.Files);
            Assert.Equal(("Scan 1.jpg", "image/jpeg", 1234L, Sha256), (original.FileName, original.MediaType, original.SizeBytes, original.Sha256));

            loaded.StartProcessing();
            loaded.AttachFile(new DocumentFile(DocumentFileKind.Archive, "archive.pdf", "application/pdf", 99, Sha256));
            loaded.Complete("Text");
            await db.SaveChangesAsync(Ct);
        }

        await using (var db = postgres.CreateContext(database))
        {
            var loaded = await db.Documents.SingleAsync(d => d.Id == document.Id, Ct);
            Assert.Equal(ProcessingState.Done, loaded.State);
            Assert.Equal("Text", loaded.ContentText);
            Assert.NotNull(loaded.FindFile(DocumentFileKind.Archive));
            Assert.Equal(s_received, (await db.ProcessingJobs.SingleAsync(Ct)).NextAttemptAt);
            Assert.Equal("Done", await ScalarAsync<string>(db, $"SELECT state AS \"Value\" FROM documents"));
        }
    }

    [Fact]
    public async Task Deleting_ADocument_RemovesItsFilesAndJob()
    {
        await using var db = await MigratedContextAsync("documents_cascade");
        var document = Document.Receive("Brief", s_received, s_owner, isPrivate: false);
        document.AttachFile(new DocumentFile(DocumentFileKind.Original, "a.pdf", "application/pdf", 1, Sha256));
        db.Documents.Add(document);
        db.ProcessingJobs.Add(new ProcessingJob { DocumentId = document.Id, NextAttemptAt = s_received });
        await db.SaveChangesAsync(Ct);

        await db.Database.ExecuteSqlAsync($"DELETE FROM documents", Ct);

        Assert.Equal(0, await ScalarAsync<int>(db, $"SELECT count(*)::int AS \"Value\" FROM document_files"));
        Assert.Equal(0, await ScalarAsync<int>(db, $"SELECT count(*)::int AS \"Value\" FROM processing_jobs"));
    }

    [Theory]
    [InlineData("german", "Rechnung")]
    [InlineData("german", "rechnungen")]
    [InlineData("english", "invoices")]
    [InlineData("german", "Stromanbieter")]
    public async Task SearchVector_FindsWordStems_InTextAndTitle(string config, string query)
    {
        await using var db = await MigratedContextAsync("documents_search_" + query.ToLowerInvariant() + "_" + config);
        var id = await AddDoneDocumentAsync(db, "Stromanbieter", "Die Rechnungen für März. Your invoice is attached.");

        var hits = await db.Database
            .SqlQuery<Guid>($"SELECT id AS \"Value\" FROM documents WHERE search_vector @@ plainto_tsquery({config}::regconfig, {query})")
            .ToListAsync(Ct);

        Assert.Equal([id], hits);
    }

    [Fact]
    public async Task Trigrams_MatchWordsDespiteOcrErrors()
    {
        await using var db = await MigratedContextAsync("documents_trigram");
        await AddDoneDocumentAsync(db, "Brief", "Die Rechnung für März");

        var similarity = await ScalarAsync<double>(
            db, $"SELECT word_similarity({"Rechnurg"}, content_text)::float8 AS \"Value\" FROM documents");

        Assert.True(similarity > 0.5, $"word_similarity was {similarity}");
    }

    private static async Task<Guid> AddDoneDocumentAsync(AppDbContext db, string title, string text)
    {
        var document = Document.Receive(title, s_received, s_owner, isPrivate: false);
        document.StartProcessing();
        document.Complete(text);
        db.Documents.Add(document);
        await db.SaveChangesAsync(Ct);
        return document.Id;
    }

    private static Task<T> ScalarAsync<T>(AppDbContext db, FormattableString sql) =>
        db.Database.SqlQuery<T>(sql).SingleAsync(Ct);

    private async Task<AppDbContext> MigratedContextAsync(string database)
    {
        var db = postgres.CreateContext(database);
        await new DatabaseMigrator(db, TimeProvider.System, NullLogger<DatabaseMigrator>.Instance).MigrateAsync(Ct);
        return db;
    }
}
