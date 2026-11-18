using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Application.Search;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Persistence;
using NoMorePaper.Infrastructure.Search;
using NoMorePaper.Infrastructure.Tests.Accounts;
using NoMorePaper.Infrastructure.Tests.Persistence;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Search;

[Collection(SharedPostgres.Name)]
public sealed class PostgresSearchIndexTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset s_received = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid s_owner = Guid.CreateVersion7();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Search_FindsInflectedForms_InGerman()
    {
        await using var db = await MigratedContextAsync("search_german");
        var invoice = await AddAsync(db, "Stromanbieter", "Die Rechnung für den Monat März.");
        await AddAsync(db, "Mietvertrag", "Die Miete beträgt 800 Euro.");

        var hits = await SearchAsync(db, "Rechnungen");

        Assert.Equal(invoice, Assert.Single(hits).Id);
    }

    [Fact]
    public async Task Search_FindsEnglishText()
    {
        await using var db = await MigratedContextAsync("search_english");
        var letter = await AddAsync(db, "Letter", "Please find the attached invoices for your records.");

        var hits = await SearchAsync(db, "invoice");

        Assert.Equal(letter, Assert.Single(hits).Id);
    }

    [Fact]
    public async Task Search_FindsByTitle_AndRanksTitleAboveText()
    {
        await using var db = await MigratedContextAsync("search_title");
        await AddAsync(db, "Brief", "Hier steht etwas über die Versicherung am Rande.");
        var byTitle = await AddAsync(db, "Versicherung", "Ganz anderer Inhalt.");

        var hits = await SearchAsync(db, "Versicherung");

        Assert.Equal(2, hits.Count);
        Assert.Equal(byTitle, hits[0].Id);
        Assert.True(hits[0].Rank >= hits[1].Rank);
    }

    [Fact]
    public async Task Search_ReturnsAPlainTextSnippet_AroundTheMatch()
    {
        await using var db = await MigratedContextAsync("search_snippet");
        var text = string.Concat(Enumerable.Repeat("Füllwörter ohne Bedeutung. ", 60)) + "Die Heizkostenabrechnung liegt bei. " + string.Concat(Enumerable.Repeat("Noch mehr Text. ", 60));
        await AddAsync(db, "Nebenkosten", text);

        var hit = Assert.Single(await SearchAsync(db, "Heizkostenabrechnung"));

        Assert.Contains("Heizkostenabrechnung", hit.Snippet, StringComparison.Ordinal);
        Assert.DoesNotContain('<', hit.Snippet);
        Assert.True(hit.Snippet.Length < 400, $"Snippet has {hit.Snippet.Length} characters");
    }

    [Fact]
    public async Task Search_FindsMisspelledWords_ThroughTrigrams()
    {
        await using var db = await MigratedContextAsync("search_trigram");
        var id = await AddAsync(db, "Brief", "Die Rechnung für den Monat März.");

        var hits = await SearchAsync(db, "Rechnnug");

        var hit = Assert.Single(hits);
        Assert.Equal(id, hit.Id);
        Assert.True(hit.Rank > 0.4);
    }

    [Fact]
    public async Task Search_DoesNotFindDocumentsThatAreNotDone()
    {
        await using var db = await MigratedContextAsync("search_state");
        db.Documents.Add(Document.Receive("Rechnung", s_received, s_owner, isPrivate: false));
        var processing = Document.Receive("Rechnung in Arbeit", s_received, s_owner, isPrivate: false);
        processing.StartProcessing();
        db.Documents.Add(processing);
        var failed = Document.Receive("Rechnung kaputt", s_received, s_owner, isPrivate: false);
        failed.StartProcessing();
        failed.Fail("OCR timed out");
        db.Documents.Add(failed);
        await db.SaveChangesAsync(Ct);

        Assert.Empty(await SearchAsync(db, "Rechnung"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\0")]
    public async Task Search_WithAnEmptyQuery_ReturnsNothing(string query)
    {
        await using var db = await MigratedContextAsync("search_empty_" + Guid.NewGuid().ToString("N")[..8]);
        await AddAsync(db, "Brief", "Text");

        Assert.Empty(await SearchAsync(db, query));
    }

    [Theory]
    [InlineData("'; DROP TABLE documents; --")]
    [InlineData("\"unbalanced quote")]
    [InlineData("a & | ! ( ) :* <->")]
    [InlineData("Rech\0nung")]
    public async Task Search_TreatsTheQueryAsText_NotAsSqlOrTsquerySyntax(string query)
    {
        await using var db = await MigratedContextAsync("search_hostile_" + Guid.NewGuid().ToString("N")[..8]);
        await AddAsync(db, "Brief", "Die Rechnung für den Monat März.");

        var exception = await Record.ExceptionAsync(() => SearchAsync(db, query));

        Assert.Null(exception);
        Assert.Single(await SearchAsync(db, "Rechnung"));
    }

    [Fact]
    public async Task Search_HonoursTheLimit()
    {
        await using var db = await MigratedContextAsync("search_limit");
        for (var i = 0; i < 5; i++)
        {
            await AddAsync(db, $"Brief {i}", "Die Rechnung liegt bei.");
        }

        Assert.Equal(2, (await SearchAsync(db, "Rechnung", limit: 2)).Count);
        Assert.Single(await SearchAsync(db, "Rechnung", limit: -3));
        Assert.Equal(5, (await SearchAsync(db, "Rechnung", limit: 5000)).Count);
    }

    private static Task<IReadOnlyList<SearchHit>> SearchAsync(AppDbContext db, string query, int limit = 20) =>
        new SearchDocuments(new PostgresSearchIndex(db, NullLogger<PostgresSearchIndex>.Instance), new FakeCurrentAccount(s_owner)).SearchAsync(query, limit, Ct);

    private static async Task<Guid> AddAsync(AppDbContext db, string title, string text)
    {
        var document = Document.Receive(title, s_received, s_owner, isPrivate: false);
        document.StartProcessing();
        document.Complete(text);
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
