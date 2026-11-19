using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Classification;
using NoMorePaper.Domain.Classification;
using NoMorePaper.Infrastructure.Classification;
using NoMorePaper.Infrastructure.Tests.Accounts;
using NoMorePaper.Infrastructure.Tests.Persistence;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Classification;

/// <summary>Merging list entries: documents move to the target, sources disappear, all or nothing.</summary>
[Collection(SharedPostgres.Name)]
public sealed class EntryMergeTests(PostgresFixture postgres)
{
    private static readonly Guid s_anna = Guid.CreateVersion7();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Merge_MovesTheDocuments_AndDeletesTheSources()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "merge_correspondents");
        var db = database.Db();
        var target = await Seed.EntryAsync(db, ClassificationKind.Correspondent, "Stadtwerke München", s_anna);
        var source = await Seed.EntryAsync(db, ClassificationKind.Correspondent, "SWM", s_anna);
        var moved = await Seed.DocumentAsync(db, s_anna, isPrivate: true, Seed.Fields(correspondentId: source));
        var kept = await Seed.DocumentAsync(db, s_anna, isPrivate: false, Seed.Fields(correspondentId: target));

        Assert.Equal(EntryChange.Changed, await Store(database).MergeAsync(s_anna, ClassificationKind.Correspondent, target, [source], Ct));

        var read = database.Db();
        Assert.Equal([target, target], await read.Documents.Where(d => d.Id == moved.Id || d.Id == kept.Id).Select(d => d.CorrespondentId!.Value).ToListAsync(Ct));
        Assert.Equal([target], await read.Correspondents.Select(c => c.Id).ToListAsync(Ct));
    }

    [Fact]
    public async Task Merge_OfTags_KeepsOneTagPerDocument()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "merge_tags");
        var db = database.Db();
        var target = await Seed.EntryAsync(db, ClassificationKind.Tag, "Strom", s_anna);
        var first = await Seed.EntryAsync(db, ClassificationKind.Tag, "Energie", s_anna);
        var second = await Seed.EntryAsync(db, ClassificationKind.Tag, "Elektrizität", s_anna);
        var both = await Seed.DocumentAsync(db, s_anna, isPrivate: false, Seed.Fields(tagIds: [target, first, second]));
        var sourceOnly = await Seed.DocumentAsync(db, s_anna, isPrivate: false, Seed.Fields(tagIds: second));

        Assert.Equal(EntryChange.Changed, await Store(database).MergeAsync(s_anna, ClassificationKind.Tag, target, [first, second], Ct));

        var read = database.Db();
        var tags = await read.Documents.AsNoTracking().Where(d => d.Id == both.Id || d.Id == sourceOnly.Id).ToDictionaryAsync(d => d.Id, d => d.Tags.Select(t => t.TagId).ToList(), Ct);
        Assert.Equal([target], tags[both.Id]);
        Assert.Equal([target], tags[sourceOnly.Id]);
        Assert.Equal([target], await read.Tags.Select(t => t.Id).ToListAsync(Ct));
    }

    [Fact]
    public async Task Merge_WithoutTheRightToEditEveryDocument_IsForbidden_AndChangesNothing()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "merge_forbidden");
        var db = database.Db();
        var bob = Guid.CreateVersion7();
        var target = await Seed.EntryAsync(db, ClassificationKind.DocumentType, "Rechnung", s_anna);
        var source = await Seed.EntryAsync(db, ClassificationKind.DocumentType, "Faktura", s_anna);
        await Seed.DocumentAsync(db, s_anna, isPrivate: false, Seed.Fields(documentTypeId: source));
        var bobs = await Seed.DocumentAsync(db, bob, isPrivate: false, Seed.Fields(documentTypeId: target));

        Assert.Equal(EntryChange.Forbidden, await Store(database).MergeAsync(s_anna, ClassificationKind.DocumentType, target, [source], Ct));

        var read = database.Db();
        Assert.Equal(2, await read.DocumentTypes.CountAsync(Ct));
        Assert.Equal(target, (await read.Documents.SingleAsync(d => d.Id == bobs.Id, Ct)).DocumentTypeId);
    }

    [Fact]
    public async Task Merge_WithAnEntryTheActorMayNotSee_IsNotFound()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "merge_hidden");
        var db = database.Db();
        var bob = Guid.CreateVersion7();
        var target = await Seed.EntryAsync(db, ClassificationKind.Tag, "Gesundheit", s_anna);
        var bobsPrivate = await Seed.EntryAsync(db, ClassificationKind.Tag, "Arzt", bob);
        await Seed.DocumentAsync(db, bob, isPrivate: true, Seed.Fields(tagIds: bobsPrivate));

        Assert.Equal(EntryChange.NotFound, await Store(database).MergeAsync(s_anna, ClassificationKind.Tag, target, [bobsPrivate], Ct));
        Assert.Equal(EntryChange.NotFound, await Store(database).MergeAsync(s_anna, ClassificationKind.Tag, Guid.CreateVersion7(), [target], Ct));
        Assert.Equal(2, await database.Db().Tags.CountAsync(Ct));
    }

    private static ClassificationStore Store(AccountDatabase database) => new(database.Db(), TimeProvider.System);
}
