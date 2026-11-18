using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Documents;
using NoMorePaper.Infrastructure.Persistence;
using NoMorePaper.Infrastructure.Search;
using NoMorePaper.Infrastructure.Tests.Persistence;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Documents;

/// <summary>The visibility rule in SQL: own documents and shared ones, never another account's private ones.</summary>
[Collection(SharedPostgres.Name)]
public sealed class DocumentVisibilityTests(PostgresFixture postgres)
{
    private const string Sha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
    private static readonly Guid s_anna = Guid.CreateVersion7();
    private static readonly Guid s_bob = Guid.CreateVersion7();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ListAndDetail_HideOtherAccountsPrivateDocuments()
    {
        await using var db = await MigratedContextAsync("visibility_list");
        var annasPrivate = await AddAsync(db, s_anna, isPrivate: true, "Kontoauszug");
        var annasShared = await AddAsync(db, s_anna, isPrivate: false, "Stromrechnung");
        var repository = new EfDocumentRepository(db);

        var forBob = await repository.ListAsync(s_bob, 1, 100, Ct);
        var forAnna = await repository.ListAsync(s_anna, 1, 100, Ct);

        Assert.Equal([annasShared], forBob.Select(d => d.Id));
        Assert.False(forBob[0].IsMine);
        Assert.Equal([annasShared, annasPrivate], forAnna.Select(d => d.Id));
        Assert.All(forAnna, d => Assert.True(d.IsMine));
        Assert.Null(await repository.GetAsync(annasPrivate, s_bob, Ct));
        Assert.True((await repository.GetAsync(annasPrivate, s_anna, Ct))!.IsPrivate);
        Assert.False((await repository.GetAsync(annasShared, s_bob, Ct))!.IsMine);
    }

    [Fact]
    public async Task Search_HidesOtherAccountsPrivateDocuments_InBothSearchModes()
    {
        await using var db = await MigratedContextAsync("visibility_search");
        var annasPrivate = await AddAsync(db, s_anna, isPrivate: true, "Geheim", "Der Kontoauszug zeigt das Gehalt.");
        var index = new PostgresSearchIndex(db, NullLogger<PostgresSearchIndex>.Instance);

        var stemForBob = await index.SearchAsync("Kontoauszug", s_bob, 10, Ct);
        var similarForBob = await index.SearchAsync("Kontoauzug", s_bob, 10, Ct);
        var stemForAnna = await index.SearchAsync("Kontoauszug", s_anna, 10, Ct);
        var similarForAnna = await index.SearchAsync("Kontoauzug", s_anna, 10, Ct);

        Assert.Empty(stemForBob);
        Assert.Empty(similarForBob);
        Assert.Equal((annasPrivate, true, true), (Assert.Single(stemForAnna).Id, stemForAnna[0].IsPrivate, stemForAnna[0].IsMine));
        Assert.Equal(annasPrivate, Assert.Single(similarForAnna).Id);
    }

    [Fact]
    public async Task SetPrivacy_WorksForTheOwnerOnly()
    {
        await using var db = await MigratedContextAsync("visibility_privacy");
        var id = await AddAsync(db, s_anna, isPrivate: false, "Brief");
        var repository = new EfDocumentRepository(db);

        Assert.False(await repository.SetPrivacyAsync(id, s_bob, isPrivate: true, Ct));
        Assert.False(await repository.SetPrivacyAsync(Guid.CreateVersion7(), s_anna, isPrivate: true, Ct));
        Assert.True(await repository.SetPrivacyAsync(id, s_anna, isPrivate: true, Ct));

        await using var read = postgres.CreateContext("visibility_privacy");
        Assert.True((await read.Documents.SingleAsync(d => d.Id == id, Ct)).IsPrivate);
    }

    private static async Task<Guid> AddAsync(AppDbContext db, Guid owner, bool isPrivate, string title, string? text = null)
    {
        var document = Document.Receive(title, DateTimeOffset.UtcNow, owner, isPrivate);
        document.AttachFile(new DocumentFile(DocumentFileKind.Original, "scan.pdf", "application/pdf", 10, Sha256));
        if (text is not null)
        {
            document.StartProcessing();
            document.Complete(text);
        }

        db.Documents.Add(document);
        await db.SaveChangesAsync(Ct);
        await Task.Delay(5, Ct);
        return document.Id;
    }

    private async Task<AppDbContext> MigratedContextAsync(string database)
    {
        var db = postgres.CreateContext(database);
        await new DatabaseMigrator(db, TimeProvider.System, NullLogger<DatabaseMigrator>.Instance).MigrateAsync(Ct);
        return db;
    }
}
