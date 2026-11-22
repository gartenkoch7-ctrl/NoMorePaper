using Xunit;
using NoMorePaper.Application.Documents;
using NoMorePaper.Domain.Classification;
using NoMorePaper.Infrastructure.Documents;
using NoMorePaper.Infrastructure.Persistence;
using NoMorePaper.Infrastructure.Tests.Accounts;
using NoMorePaper.Infrastructure.Tests.Classification;
using NoMorePaper.Infrastructure.Tests.Persistence;

namespace NoMorePaper.Infrastructure.Tests.Documents;

/// <summary>The counts of the document list: visibility first, filters narrow, the type chips ignore the type filter.</summary>
[Collection(SharedPostgres.Name)]
public sealed class DocumentCountsTests(PostgresFixture postgres)
{
    private static readonly Guid s_anna = Guid.CreateVersion7();
    private static readonly Guid s_bob = Guid.CreateVersion7();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PrivateDocumentsOfOtherAccounts_AreNeverCounted()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "counts_privacy");
        var db = database.Db();
        var invoice = await Seed.EntryAsync(db, ClassificationKind.DocumentType, "Rechnung", s_anna);
        await Seed.DocumentAsync(db, s_anna, isPrivate: false, Seed.Fields(null, invoice));
        await Seed.DocumentAsync(db, s_anna, isPrivate: true, Seed.Fields(null, invoice));
        await Seed.DocumentAsync(db, s_bob, isPrivate: true, Seed.Fields(null, invoice));
        var counts = new EfDocumentCounts(db);

        var forBob = await counts.CountAsync(s_bob, DocumentFilter.None, Ct);
        var forAnna = await counts.CountAsync(s_anna, DocumentFilter.None, Ct);
        var forStranger = await counts.CountAsync(Guid.CreateVersion7(), DocumentFilter.None, Ct);

        Assert.Equal((2, 2, 2), (forBob.Total, forBob.AllTypes, Assert.Single(forBob.ByType).Count));
        Assert.Equal((2, 2, 2), (forAnna.Total, forAnna.AllTypes, Assert.Single(forAnna.ByType).Count));
        Assert.Equal((1, 1, 1), (forStranger.Total, forStranger.AllTypes, Assert.Single(forStranger.ByType).Count));
        var privateOnly = await counts.CountAsync(s_bob, new DocumentFilter(IsPrivate: true), Ct);
        Assert.Equal((1, 1), (privateOnly.Total, privateOnly.ByType.Sum(item => item.Count)));
    }

    [Fact]
    public async Task ATypeOnlyOnAnotherAccountsPrivateDocument_IsNotInTheChips()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "counts_secret_type");
        var db = database.Db();
        var secret = await Seed.EntryAsync(db, ClassificationKind.DocumentType, "Geheim", s_anna);
        await Seed.DocumentAsync(db, s_anna, isPrivate: true, Seed.Fields(null, secret));
        var counts = new EfDocumentCounts(db);

        var forBob = await counts.CountAsync(s_bob, new DocumentFilter(DocumentTypeId: secret), Ct);
        var forAnna = await counts.CountAsync(s_anna, DocumentFilter.None, Ct);

        Assert.Equal((0, 0), (forBob.Total, forBob.AllTypes));
        Assert.Empty(forBob.ByType);
        Assert.Equal("Geheim", Assert.Single(forAnna.ByType).Type.Name);
    }

    [Fact]
    public async Task CorrespondentFilter_NarrowsTheTotal()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "counts_filter");
        var db = database.Db();
        var sender = await Seed.EntryAsync(db, ClassificationKind.Correspondent, "Stadtwerke", s_anna);
        await Seed.DocumentAsync(db, s_anna, isPrivate: false, Seed.Fields(sender));
        await Seed.DocumentAsync(db, s_anna, isPrivate: false);

        var counts = await new EfDocumentCounts(db).CountAsync(s_anna, new DocumentFilter(CorrespondentId: sender), Ct);

        Assert.Equal((1, 1), (counts.Total, counts.AllTypes));
    }

    [Fact]
    public async Task TypeFilter_NarrowsTheTotal_ButNotTheChips()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "counts_type");
        var db = database.Db();
        var invoice = await Seed.EntryAsync(db, ClassificationKind.DocumentType, "Rechnung", s_anna);
        var contract = await Seed.EntryAsync(db, ClassificationKind.DocumentType, "Vertrag", s_anna);
        await Seed.DocumentAsync(db, s_anna, isPrivate: false, Seed.Fields(null, invoice));
        await Seed.DocumentAsync(db, s_anna, isPrivate: false, Seed.Fields(null, invoice));
        await Seed.DocumentAsync(db, s_anna, isPrivate: false, Seed.Fields(null, contract));

        var counts = await new EfDocumentCounts(db).CountAsync(s_anna, new DocumentFilter(DocumentTypeId: invoice), Ct);

        Assert.Equal((2, 3), (counts.Total, counts.AllTypes));
        Assert.Equal([("Rechnung", 2), ("Vertrag", 1)], counts.ByType.Select(item => (item.Type.Name, item.Count)));
    }

    [Fact]
    public async Task TypesWithoutDocuments_AreLeftOut_AndTheOthersSortedByName()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "counts_sorted");
        var db = database.Db();
        var zeta = await Seed.EntryAsync(db, ClassificationKind.DocumentType, "Zeugnis", s_anna);
        var alpha = await Seed.EntryAsync(db, ClassificationKind.DocumentType, "Antrag", s_anna);
        await Seed.EntryAsync(db, ClassificationKind.DocumentType, "Leer", s_anna);
        await Seed.DocumentAsync(db, s_anna, isPrivate: false, Seed.Fields(null, zeta));
        await Seed.DocumentAsync(db, s_anna, isPrivate: false, Seed.Fields(null, alpha));

        var counts = await new EfDocumentCounts(db).CountAsync(s_anna, DocumentFilter.None, Ct);

        Assert.Equal(["Antrag", "Zeugnis"], counts.ByType.Select(item => item.Type.Name));
    }

    [Fact]
    public async Task EmptyDatabase_CountsZero()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "counts_empty");

        var counts = await new EfDocumentCounts(database.Db()).CountAsync(s_anna, DocumentFilter.None, Ct);

        Assert.Equal((0, 0), (counts.Total, counts.AllTypes));
        Assert.Empty(counts.ByType);
    }
}
