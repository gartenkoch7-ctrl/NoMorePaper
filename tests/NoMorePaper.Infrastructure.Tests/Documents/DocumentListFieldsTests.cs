using Microsoft.EntityFrameworkCore;
using Xunit;
using NoMorePaper.Application.Documents;
using NoMorePaper.Domain.Classification;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Documents;
using NoMorePaper.Infrastructure.Persistence;
using NoMorePaper.Infrastructure.Tests.Accounts;
using NoMorePaper.Infrastructure.Tests.Classification;
using NoMorePaper.Infrastructure.Tests.Persistence;

namespace NoMorePaper.Infrastructure.Tests.Documents;

/// <summary>The extra list fields: source, excerpt and tags of the documents on the page.</summary>
[Collection(SharedPostgres.Name)]
public sealed class DocumentListFieldsTests(PostgresFixture postgres)
{
    private static readonly Guid s_anna = Guid.CreateVersion7();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Source_IsListed()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "fields_source");
        var db = database.Db();
        var mail = Document.Receive("Post", DateTimeOffset.UtcNow, s_anna, isPrivate: false, DocumentSource.Mail);
        db.Documents.Add(mail);
        await db.SaveChangesAsync(Ct);

        var rows = await new EfDocumentRepository(db).ListAsync(s_anna, 1, 10, DocumentFilter.None, Ct);

        Assert.Equal(DocumentSource.Mail, Assert.Single(rows).Source);
    }

    [Fact]
    public async Task Excerpt_PrefersTheSummary_ThenTheText_ThenNothing()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "fields_excerpt");
        var db = database.Db();
        var withSummary = await Seed.DocumentAsync(db, s_anna, isPrivate: false, Seed.Fields(), "Voller OCR Text");
        var textOnly = await Seed.DocumentAsync(db, s_anna, isPrivate: false, text: "Nur   der\nText");
        var none = await Seed.DocumentAsync(db, s_anna, isPrivate: false);

        var rows = (await new EfDocumentRepository(db).ListAsync(s_anna, 1, 10, DocumentFilter.None, Ct)).ToDictionary(row => row.Id);

        Assert.Equal("Kurz.", rows[withSummary.Id].Excerpt);
        Assert.Equal("Nur der Text", rows[textOnly.Id].Excerpt);
        Assert.Null(rows[none.Id].Excerpt);
    }

    [Fact]
    public async Task Excerpt_CollapsesWhitespaceInSql_AndIgnoresASummaryOfOnlyLineBreaks()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "fields_blanks");
        var db = database.Db();
        var spaced = await Seed.DocumentAsync(db, s_anna, isPrivate: false, text: new string('\n', 600) + "Anfang   des\t\tTextes");
        var blank = await Seed.DocumentAsync(db, s_anna, isPrivate: false, text: "Der Text");
        await db.Database.ExecuteSqlRawAsync("UPDATE documents SET summary = E'\\n \\r\\n' WHERE id = {0}", [blank.Id], Ct);

        var rows = (await new EfDocumentRepository(db).ListAsync(s_anna, 1, 10, DocumentFilter.None, Ct)).ToDictionary(row => row.Id);

        Assert.Equal("Anfang des Textes", rows[spaced.Id].Excerpt);
        Assert.Equal("Der Text", rows[blank.Id].Excerpt);
    }

    [Fact]
    public async Task Tags_AreSortedByName_AndEmptyWithoutTags()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "fields_tags");
        var db = database.Db();
        var strom = await Seed.EntryAsync(db, ClassificationKind.Tag, "Strom", s_anna);
        var gas = await Seed.EntryAsync(db, ClassificationKind.Tag, "Gas", s_anna);
        var tagged = await Seed.DocumentAsync(db, s_anna, isPrivate: false, Seed.Fields(null, null, null, null, strom, gas));
        var plain = await Seed.DocumentAsync(db, s_anna, isPrivate: false);

        var rows = (await new EfDocumentRepository(db).ListAsync(s_anna, 1, 10, DocumentFilter.None, Ct)).ToDictionary(row => row.Id);

        Assert.Equal(["Gas", "Strom"], rows[tagged.Id].Tags.Select(tag => tag.Name));
        Assert.Empty(rows[plain.Id].Tags);
    }

    [Fact]
    public async Task Tags_OfDocumentsOnAnotherPage_AreNotLoaded()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "fields_page");
        var db = database.Db();
        var strom = await Seed.EntryAsync(db, ClassificationKind.Tag, "Strom", s_anna);
        var older = await Seed.DocumentAsync(db, s_anna, isPrivate: false, Seed.Fields(null, null, null, null, strom));
        var newer = await Seed.DocumentAsync(db, s_anna, isPrivate: false);

        var firstPage = await new EfDocumentRepository(db).ListAsync(s_anna, 1, 1, DocumentFilter.None, Ct);
        var secondPage = await new EfDocumentRepository(db).ListAsync(s_anna, 2, 1, DocumentFilter.None, Ct);

        Assert.Equal((newer.Id, 0), (Assert.Single(firstPage).Id, firstPage[0].Tags.Count));
        Assert.Equal((older.Id, 1), (Assert.Single(secondPage).Id, secondPage[0].Tags.Count));
    }
}
