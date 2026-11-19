using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Domain.Classification;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Classification;
using NoMorePaper.Infrastructure.Persistence;
using NoMorePaper.Infrastructure.Tests.Persistence;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Classification;

/// <summary>
/// The visibility rule of list entries in SQL (<see cref="VisibilitySql"/>): an entry used only in
/// private documents never reaches another account; unused entries reach only their creator.
/// </summary>
[Collection(SharedPostgres.Name)]
public sealed class EntryVisibilityTests(PostgresFixture postgres)
{
    private static readonly Guid s_anna = Guid.CreateVersion7();
    private static readonly Guid s_bob = Guid.CreateVersion7();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(ClassificationKind.Correspondent)]
    [InlineData(ClassificationKind.DocumentType)]
    [InlineData(ClassificationKind.Tag)]
    public async Task AnEntryUsedOnlyInPrivateDocuments_IsInvisibleToOthers(ClassificationKind kind)
    {
        await using var db = await MigratedContextAsync("entries_private_" + kind.ToString().ToLowerInvariant());
        var entry = await AddEntryAsync(db, kind, "Arztpraxis Dr. Huber", s_anna);
        await AddDocumentAsync(db, s_anna, isPrivate: true, kind, entry);

        Assert.Equal(["Arztpraxis Dr. Huber"], await VisibleNamesAsync(db, kind, s_anna));
        Assert.Empty(await VisibleNamesAsync(db, kind, s_bob));
    }

    [Theory]
    [InlineData(ClassificationKind.Correspondent)]
    [InlineData(ClassificationKind.DocumentType)]
    [InlineData(ClassificationKind.Tag)]
    public async Task AnEntry_BecomesVisibleToEverybody_OnceASharedDocumentUsesIt(ClassificationKind kind)
    {
        await using var db = await MigratedContextAsync("entries_shared_" + kind.ToString().ToLowerInvariant());
        var entry = await AddEntryAsync(db, kind, "Stadtwerke", s_anna);
        await AddDocumentAsync(db, s_anna, isPrivate: true, kind, entry);
        await AddDocumentAsync(db, s_anna, isPrivate: false, kind, entry);

        Assert.Equal(["Stadtwerke"], await VisibleNamesAsync(db, kind, s_bob));
        Assert.Equal(["Stadtwerke"], await VisibleNamesAsync(db, kind, s_anna));
    }

    [Fact]
    public async Task SharingThePrivateDocument_ShowsItsEntries_AndMakingItPrivateHidesThemAgain()
    {
        await using var db = await MigratedContextAsync("entries_privacy_change");
        var entry = await AddEntryAsync(db, ClassificationKind.Correspondent, "Finanzamt", s_anna);
        var document = await AddDocumentAsync(db, s_anna, isPrivate: true, ClassificationKind.Correspondent, entry);

        document.Share(s_anna);
        await db.SaveChangesAsync(Ct);
        Assert.Equal(["Finanzamt"], await VisibleNamesAsync(db, ClassificationKind.Correspondent, s_bob));

        document.MakePrivate(s_anna);
        await db.SaveChangesAsync(Ct);
        Assert.Empty(await VisibleNamesAsync(db, ClassificationKind.Correspondent, s_bob));
    }

    [Theory]
    [InlineData(ClassificationKind.Correspondent)]
    [InlineData(ClassificationKind.DocumentType)]
    [InlineData(ClassificationKind.Tag)]
    public async Task AnUnusedEntry_IsVisibleToItsCreatorOnly(ClassificationKind kind)
    {
        await using var db = await MigratedContextAsync("entries_unused_" + kind.ToString().ToLowerInvariant());
        await AddEntryAsync(db, kind, "Neu", s_anna);

        Assert.Equal(["Neu"], await VisibleNamesAsync(db, kind, s_anna));
        Assert.Empty(await VisibleNamesAsync(db, kind, s_bob));
    }

    [Fact]
    public async Task ACreator_DoesNotSeeTheEntry_WhenOnlyAnotherAccountsPrivateDocumentUsesIt()
    {
        // Otherwise Bob would learn that one of Anna's private documents uses his entry.
        await using var db = await MigratedContextAsync("entries_creator_private_use");
        var entry = await AddEntryAsync(db, ClassificationKind.Tag, "Gesundheit", s_bob);
        await AddDocumentAsync(db, s_anna, isPrivate: true, ClassificationKind.Tag, entry);

        Assert.Empty(await VisibleNamesAsync(db, ClassificationKind.Tag, s_bob));
        Assert.Equal(["Gesundheit"], await VisibleNamesAsync(db, ClassificationKind.Tag, s_anna));
    }

    [Fact]
    public async Task DocumentsWithoutOwner_CountAsShared()
    {
        await using var db = await MigratedContextAsync("entries_without_owner");
        var entry = await AddEntryAsync(db, ClassificationKind.Correspondent, "Altbestand", s_anna);
        await db.Database.ExecuteSqlAsync(
            $"INSERT INTO documents (id, title, received_at, state, is_private, recognition, correspondent_id) VALUES ({Guid.CreateVersion7()}, 'alt', now(), 'Done', false, 'None', {entry})",
            Ct);

        Assert.Equal(["Altbestand"], await VisibleNamesAsync(db, ClassificationKind.Correspondent, s_bob));
    }

    [Fact]
    public async Task TheRule_ComposesWithFurtherFilters()
    {
        await using var db = await MigratedContextAsync("entries_compose");
        await AddEntryAsync(db, ClassificationKind.Correspondent, "Stadtwerke München GmbH", s_anna);
        await AddEntryAsync(db, ClassificationKind.Correspondent, "Finanzamt", s_anna);
        var normalized = ClassificationName.Normalize("stadtwerke-münchen");

        var found = await db.Correspondents
            .FromSql(VisibilitySql.Entries(ClassificationKind.Correspondent, s_anna))
            .Where(entry => entry.NormalizedName == normalized)
            .Select(entry => entry.Name)
            .ToListAsync(Ct);

        Assert.Equal(["Stadtwerke München GmbH"], found);
    }

    private static async Task<Guid> AddEntryAsync(AppDbContext db, ClassificationKind kind, string name, Guid createdBy)
    {
        ClassificationEntry entry = kind switch
        {
            ClassificationKind.Correspondent => db.Correspondents.Add(Correspondent.Create(name, createdBy, DateTimeOffset.UtcNow)).Entity,
            ClassificationKind.DocumentType => db.DocumentTypes.Add(DocumentType.Create(name, createdBy, DateTimeOffset.UtcNow)).Entity,
            _ => db.Tags.Add(Tag.Create(name, createdBy, DateTimeOffset.UtcNow)).Entity,
        };
        await db.SaveChangesAsync(Ct);
        return entry.Id;
    }

    private static async Task<Document> AddDocumentAsync(AppDbContext db, Guid owner, bool isPrivate, ClassificationKind kind, Guid entryId)
    {
        var document = Document.Receive("Brief", DateTimeOffset.UtcNow, owner, isPrivate);
        document.Classify(new DocumentFields(
            null,
            kind == ClassificationKind.Correspondent ? entryId : null,
            kind == ClassificationKind.DocumentType ? entryId : null,
            null,
            null,
            null,
            kind == ClassificationKind.Tag ? [entryId] : []));
        db.Documents.Add(document);
        await db.SaveChangesAsync(Ct);
        return document;
    }

    private static Task<List<string>> VisibleNamesAsync(AppDbContext db, ClassificationKind kind, Guid viewer)
    {
        var sql = VisibilitySql.Entries(kind, viewer);
        var names = kind switch
        {
            ClassificationKind.Correspondent => db.Correspondents.FromSql(sql).Select(entry => entry.Name),
            ClassificationKind.DocumentType => db.DocumentTypes.FromSql(sql).Select(entry => entry.Name),
            _ => db.Tags.FromSql(sql).Select(entry => entry.Name),
        };
        return names.OrderBy(name => name).ToListAsync(Ct);
    }

    private async Task<AppDbContext> MigratedContextAsync(string database)
    {
        var db = postgres.CreateContext(database);
        await new DatabaseMigrator(db, TimeProvider.System, NullLogger<DatabaseMigrator>.Instance).MigrateAsync(Ct);
        return db;
    }
}
