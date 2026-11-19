using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Application.Documents;
using NoMorePaper.Domain.Classification;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Documents;
using NoMorePaper.Infrastructure.Persistence;
using NoMorePaper.Infrastructure.Search;
using NoMorePaper.Infrastructure.Tests.Accounts;
using NoMorePaper.Infrastructure.Tests.Classification;
using NoMorePaper.Infrastructure.Tests.Persistence;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Documents;

/// <summary>The filters of list and search, and the fields they show, against Postgres.</summary>
[Collection(SharedPostgres.Name)]
public sealed class DocumentFilterTests(PostgresFixture postgres)
{
    private static readonly Guid s_anna = Guid.CreateVersion7();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EachFilter_NarrowsTheList_AndRowsCarryTheirFields()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "filter_list");
        var (db, set) = await SeedAsync(database);

        Assert.Equal([set.Invoice], await ListAsync(db, new DocumentFilter(CorrespondentId: set.Sender)));
        Assert.Equal([set.Letter, set.Invoice], await ListAsync(db, new DocumentFilter(DocumentTypeId: set.Type)));
        Assert.Equal([set.Letter], await ListAsync(db, new DocumentFilter(From: new DateOnly(2026, 6, 1))));
        Assert.Equal([set.Invoice], await ListAsync(db, new DocumentFilter(To: new DateOnly(2026, 5, 31))));
        Assert.Equal([set.Letter, set.Invoice], await ListAsync(db, new DocumentFilter(TagIds: [set.Strom, set.Gas])));
        Assert.Equal([set.Invoice], await ListAsync(db, new DocumentFilter(TagIds: [set.Strom, set.Gas], AllTags: true)));
        Assert.Equal([set.Letter], await ListAsync(db, new DocumentFilter(AmountFrom: 100m)));
        Assert.Equal([set.Invoice], await ListAsync(db, new DocumentFilter(AmountTo: 84.2m)));
        Assert.Equal([set.Letter], await ListAsync(db, new DocumentFilter(IsPrivate: true)));
        Assert.Equal([set.Plain, set.Invoice], await ListAsync(db, new DocumentFilter(IsPrivate: false)));
        Assert.Equal([set.Invoice], await ListAsync(db, new DocumentFilter(DocumentTypeId: set.Type, AmountTo: 90m, IsPrivate: false)));

        var invoice = (await new EfDocumentRepository(db).ListAsync(s_anna, 1, 10, DocumentFilter.None, Ct)).Single(row => row.Id == set.Invoice);
        Assert.Equal(("Stadtwerke", "Rechnung"), (invoice.Labels.Correspondent?.Name, invoice.Labels.DocumentType?.Name));
        Assert.Equal((new DateOnly(2026, 5, 1), new Money(84.2m, "EUR"), RecognitionState.Automatic), (invoice.Labels.DocumentDate, invoice.Labels.Amount, invoice.Labels.Recognition));
    }

    [Fact]
    public async Task Filters_NeverWidenWhatTheViewerMaySee()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "filter_privacy");
        var (db, set) = await SeedAsync(database);
        var bob = Guid.CreateVersion7();

        Assert.Empty(await new EfDocumentRepository(db).ListAsync(bob, 1, 10, new DocumentFilter(IsPrivate: true), Ct));
        Assert.DoesNotContain(set.Letter, (await new EfDocumentRepository(db).ListAsync(bob, 1, 10, new DocumentFilter(DocumentTypeId: set.Type), Ct)).Select(r => r.Id));
    }

    [Fact]
    public async Task Search_AppliesTheSameFilters_InBothSearchModes()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "filter_search");
        var (db, set) = await SeedAsync(database);
        var index = new PostgresSearchIndex(db, NullLogger<PostgresSearchIndex>.Instance);

        var stem = await index.SearchAsync("Abrechnung", s_anna, 10, new DocumentFilter(CorrespondentId: set.Sender), Ct);
        var similar = await index.SearchAsync("Abrechnug", s_anna, 10, new DocumentFilter(TagIds: [set.Gas], AllTags: true), Ct);

        var hit = Assert.Single(stem);
        Assert.Equal((set.Invoice, "Stadtwerke", new Money(84.2m, "EUR")), (hit.Id, hit.Labels.Correspondent?.Name, hit.Labels.Amount));
        Assert.Equal([set.Invoice], similar.Select(h => h.Id));
        Assert.Equal(3, (await index.SearchAsync("Abrechnung", s_anna, 10, DocumentFilter.None, Ct)).Count);
    }

    [Fact]
    public async Task SameFile_IsFound_OnlyAmongTheViewersDocuments()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "filter_sha");
        var db = database.Db();
        var bob = Guid.CreateVersion7();
        var shared = new string('a', 64);
        var secret = new string('b', 64);
        var first = await Seed.DocumentAsync(db, s_anna, isPrivate: false, sha256: shared);
        await Seed.DocumentAsync(db, s_anna, isPrivate: false, sha256: shared);
        await Seed.DocumentAsync(db, s_anna, isPrivate: true, sha256: secret);
        var repository = new EfDocumentRepository(db);

        Assert.Equal(first.Id, await repository.FindVisibleBySha256Async(bob, shared, Ct));
        Assert.Null(await repository.FindVisibleBySha256Async(bob, secret, Ct));
        Assert.NotNull(await repository.FindVisibleBySha256Async(s_anna, secret, Ct));
        Assert.Null(await repository.FindVisibleBySha256Async(s_anna, new string('c', 64), Ct));
    }

    // Plain: shared, no fields. Invoice: shared, Stadtwerke, Rechnung, 2026-05-01, 84.20 EUR, Strom + Gas.
    // Letter: private, Rechnung, 2026-07-01, 120 EUR, Strom. All three mention "Abrechnung".
    private static async Task<(AppDbContext Db, SeedSet Set)> SeedAsync(AccountDatabase database)
    {
        var db = database.Db();
        var sender = await Seed.EntryAsync(db, ClassificationKind.Correspondent, "Stadtwerke", s_anna);
        var type = await Seed.EntryAsync(db, ClassificationKind.DocumentType, "Rechnung", s_anna);
        var strom = await Seed.EntryAsync(db, ClassificationKind.Tag, "Strom", s_anna);
        var gas = await Seed.EntryAsync(db, ClassificationKind.Tag, "Gas", s_anna);
        var invoice = await Seed.DocumentAsync(
            db, s_anna, isPrivate: false, Seed.Fields(sender, type, new DateOnly(2026, 5, 1), new Money(84.2m, "EUR"), strom, gas), "Abrechnung Strom und Gas");
        var letter = await Seed.DocumentAsync(
            db, s_anna, isPrivate: true, Seed.Fields(null, type, new DateOnly(2026, 7, 1), new Money(120m, "EUR"), strom), "Abrechnung Strom");
        var plain = await Seed.DocumentAsync(db, s_anna, isPrivate: false, text: "Abrechnung ohne Angaben");
        return (db, new SeedSet(sender, type, strom, gas, invoice.Id, letter.Id, plain.Id));
    }

    private static async Task<List<Guid>> ListAsync(AppDbContext db, DocumentFilter filter) =>
        [.. (await new EfDocumentRepository(db).ListAsync(s_anna, 1, 10, filter, Ct)).Select(row => row.Id)];

    private sealed record SeedSet(Guid Sender, Guid Type, Guid Strom, Guid Gas, Guid Invoice, Guid Letter, Guid Plain);
}
