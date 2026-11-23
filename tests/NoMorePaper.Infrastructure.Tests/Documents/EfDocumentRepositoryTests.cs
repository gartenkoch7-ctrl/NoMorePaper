using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Application.Documents;
using NoMorePaper.Infrastructure.Tests.Accounts;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Documents;
using NoMorePaper.Infrastructure.Persistence;
using NoMorePaper.Infrastructure.Tests.Persistence;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Documents;

[Collection(SharedPostgres.Name)]
public sealed class EfDocumentRepositoryTests(PostgresFixture postgres)
{
    private const string Sha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
    private static readonly DateTimeOffset s_start = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid s_owner = Guid.CreateVersion7();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task List_ReturnsNewestFirst_InPages()
    {
        await using var db = await MigratedContextAsync("queries_list");
        var ids = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            ids.Add(await AddAsync(db, $"Dokument {i}", s_start.AddMinutes(i)));
        }

        var queries = new ListDocuments(new EfDocumentRepository(db), new FakeCurrentAccount(s_owner));
        var first = await queries.ListAsync(1, 2, DocumentFilter.None, Ct);
        var second = await queries.ListAsync(2, 2, DocumentFilter.None, Ct);
        var last = await queries.ListAsync(3, 2, DocumentFilter.None, Ct);

        Assert.Equal([ids[4], ids[3]], first.Select(d => d.Id));
        Assert.Equal([ids[2], ids[1]], second.Select(d => d.Id));
        Assert.Equal([ids[0]], last.Select(d => d.Id));
        Assert.Equal(("Dokument 4", s_start.AddMinutes(4), ProcessingState.Queued), (first[0].Title, first[0].ReceivedAt, first[0].State));
    }

    [Fact]
    public async Task List_ClampsPageAndPageSize()
    {
        await using var db = await MigratedContextAsync("queries_clamp");
        for (var i = 0; i < 3; i++)
        {
            await AddAsync(db, $"Dokument {i}", s_start.AddMinutes(i));
        }

        var queries = new ListDocuments(new EfDocumentRepository(db), new FakeCurrentAccount(s_owner));

        Assert.Single(await queries.ListAsync(0, 0, DocumentFilter.None, Ct));
        Assert.Single(await queries.ListAsync(-7, -1, DocumentFilter.None, Ct));
        Assert.Equal(3, (await queries.ListAsync(1, 100_000, DocumentFilter.None, Ct)).Count);
        Assert.Empty(await queries.ListAsync(int.MaxValue, 100, DocumentFilter.None, Ct));
    }

    [Fact]
    public async Task Get_ReturnsTheDocumentWithItsFiles()
    {
        await using var db = await MigratedContextAsync("queries_get");
        var id = await AddAsync(db, "Brief", s_start);
        var document = await db.Documents.FindAsync([id], Ct);
        document!.StartProcessing();
        document.AttachFile(new DocumentFile(DocumentFileKind.Archive, "archive.pdf", "application/pdf", 99, Sha256));
        document.Complete("Text");
        await db.SaveChangesAsync(Ct);

        await using var read = postgres.CreateContext("queries_get");
        var detail = await new GetDocument(new EfDocumentRepository(read), new FakeCurrentAccount(s_owner)).GetAsync(id, Ct);

        Assert.NotNull(detail);
        Assert.Equal((id, "Brief", ProcessingState.Done, null), (detail.Id, detail.Title, detail.State, detail.FailureReason));
        Assert.Equal("scan.pdf", detail.Original?.FileName);
        Assert.Equal("archive.pdf", detail.Archive?.FileName);
    }

    [Fact]
    public async Task Get_HasNoArchive_BeforeProcessing_AndReportsFailures()
    {
        await using var db = await MigratedContextAsync("queries_failed");
        var id = await AddAsync(db, "Kaputt", s_start);
        var document = await db.Documents.FindAsync([id], Ct);
        document!.StartProcessing();
        document.Fail("OCR timed out");
        await db.SaveChangesAsync(Ct);

        var detail = await new GetDocument(new EfDocumentRepository(db), new FakeCurrentAccount(s_owner)).GetAsync(id, Ct);

        Assert.Null(detail!.Archive);
        Assert.Equal((ProcessingState.Failed, "OCR timed out"), (detail.State, detail.FailureReason));
    }

    [Fact]
    public async Task Get_OfAnUnknownId_ReturnsNull()
    {
        await using var db = await MigratedContextAsync("queries_unknown");

        Assert.Null(await new GetDocument(new EfDocumentRepository(db), new FakeCurrentAccount(s_owner)).GetAsync(Guid.CreateVersion7(), Ct));
    }

    private static async Task<Guid> AddAsync(AppDbContext db, string title, DateTimeOffset receivedAt)
    {
        var document = Document.Receive(title, receivedAt, s_owner, isPrivate: false);
        document.AttachFile(new DocumentFile(DocumentFileKind.Original, "scan.pdf", "application/pdf", 10, Sha256));
        db.Documents.Add(document);
        await db.SaveChangesAsync(Ct);
        return document.Id;
    }

    private async Task<AppDbContext> MigratedContextAsync(string database)
    {
        var db = postgres.CreateContext(database);
        await new DatabaseMigrator(db, TimeProvider.System, NullLogger<DatabaseMigrator>.Instance).MigrateAsync(Ct);
        return db;
    }
}
