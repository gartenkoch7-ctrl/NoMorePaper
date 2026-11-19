using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Classification;
using NoMorePaper.Domain.Classification;
using NoMorePaper.Infrastructure.Classification;
using NoMorePaper.Infrastructure.Tests.Accounts;
using NoMorePaper.Infrastructure.Tests.Persistence;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Classification;

/// <summary>
/// The lists against Postgres: finding by normalized name only among visible entries, and renaming or
/// merging only for an account that may edit every affected document.
/// </summary>
[Collection(SharedPostgres.Name)]
public sealed class ClassificationStoreTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task FindOrCreate_FindsAVisibleEntryByNormalizedName_ButNeverAnotherAccountsPrivateOne()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "store_find");
        var anna = Guid.CreateVersion7();
        var bob = Guid.CreateVersion7();

        var created = await Store(database).FindOrCreateAsync(anna, ClassificationKind.Correspondent, "Stadtwerke München GmbH", EntryAudience.Account, Ct);
        var again = await Store(database).FindOrCreateAsync(anna, ClassificationKind.Correspondent, "  stadtwerke-munchen ", EntryAudience.Account, Ct);
        var bobs = await Store(database).FindOrCreateAsync(bob, ClassificationKind.Correspondent, "Stadtwerke München", EntryAudience.Account, Ct);

        Assert.Equal(created, again);
        Assert.NotEqual(created.Id, bobs.Id);
        Assert.Equal("Stadtwerke München", bobs.Name);
        Assert.Equal([created], await Store(database).VisibleEntriesAsync(anna, ClassificationKind.Correspondent, EntryAudience.Account, Ct));
    }

    [Fact]
    public async Task FindOrCreate_ForTheHousehold_ReusesOnlyAnEntryASharedDocumentUses()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "store_find_household");
        var anna = Guid.CreateVersion7();
        var db = database.Db();
        var therapy = await Seed.EntryAsync(db, ClassificationKind.Tag, "Psychotherapie", anna);
        await Seed.DocumentAsync(db, anna, isPrivate: true, Seed.Fields(tagIds: therapy));
        var power = await Seed.EntryAsync(db, ClassificationKind.Tag, "Strom", anna);
        await Seed.DocumentAsync(db, anna, isPrivate: false, Seed.Fields(tagIds: power));

        var forShared = await Store(database).FindOrCreateAsync(anna, ClassificationKind.Tag, "psychotherapie", EntryAudience.Household, Ct);
        var forPrivate = await Store(database).FindOrCreateAsync(anna, ClassificationKind.Tag, "psychotherapie", EntryAudience.Account, Ct);
        var shared = await Store(database).FindOrCreateAsync(anna, ClassificationKind.Tag, "Strom", EntryAudience.Household, Ct);

        // A second entry of the same name: the private one stays private, nothing collides.
        Assert.NotEqual(therapy, forShared.Id);
        Assert.Equal("psychotherapie", forShared.Name);
        Assert.Equal(therapy, forPrivate.Id);
        Assert.Equal(power, shared.Id);
        Assert.Equal(["Strom"], (await Store(database).VisibleEntriesAsync(anna, ClassificationKind.Tag, EntryAudience.Household, Ct)).Select(entry => entry.Name));
    }

    [Fact]
    public async Task FindOrCreate_RefusesAnInvalidName()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "store_invalid");

        await Assert.ThrowsAsync<ArgumentException>(() => Store(database).FindOrCreateAsync(Guid.CreateVersion7(), ClassificationKind.Tag, " ", EntryAudience.Account, Ct));
    }

    [Fact]
    public async Task VisibleEntries_AreOrderedByName()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "store_order");
        var anna = Guid.CreateVersion7();
        foreach (var name in new[] { "Strom", "Auto", "Miete" })
        {
            await Store(database).FindOrCreateAsync(anna, ClassificationKind.Tag, name, EntryAudience.Account, Ct);
        }

        var names = (await Store(database).VisibleEntriesAsync(anna, ClassificationKind.Tag, EntryAudience.Account, Ct)).Select(entry => entry.Name);

        Assert.Equal(["Auto", "Miete", "Strom"], names);
    }

    [Theory]
    [InlineData(ClassificationKind.Correspondent)]
    [InlineData(ClassificationKind.DocumentType)]
    [InlineData(ClassificationKind.Tag)]
    public async Task Rename_IsAllowed_ForTheOwnerOfEveryDocumentUsingIt(ClassificationKind kind)
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "store_rename_" + kind.ToString().ToLowerInvariant());
        var anna = Guid.CreateVersion7();
        var db = database.Db();
        var entry = await Seed.EntryAsync(db, kind, "Stadwerke", anna);
        await Seed.DocumentAsync(db, anna, isPrivate: false, Uses(kind, entry));
        await Seed.DocumentAsync(db, anna, isPrivate: true, Uses(kind, entry));

        Assert.Equal(EntryChange.Changed, await Store(database).RenameAsync(anna, kind, entry, "Stadtwerke AG", Ct));

        var renamed = Assert.Single(await Store(database).VisibleEntriesAsync(anna, kind, EntryAudience.Account, Ct));
        Assert.Equal("Stadtwerke AG", renamed.Name);
        var read = database.Db();
        IQueryable<ClassificationEntry> entries = kind switch
        {
            ClassificationKind.Correspondent => read.Correspondents,
            ClassificationKind.DocumentType => read.DocumentTypes,
            _ => read.Tags,
        };
        Assert.Equal("stadtwerke", await entries.Where(e => e.Id == entry).Select(e => e.NormalizedName).SingleAsync(Ct));
    }

    [Fact]
    public async Task Rename_IsForbidden_WhenADocumentTheActorMayNotEditUsesIt()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "store_rename_forbidden");
        var anna = Guid.CreateVersion7();
        var bob = Guid.CreateVersion7();
        var db = database.Db();
        var entry = await Seed.EntryAsync(db, ClassificationKind.Tag, "Strom", anna);
        await Seed.DocumentAsync(db, anna, isPrivate: false, Seed.Fields(tagIds: entry));
        await Seed.DocumentAsync(db, bob, isPrivate: true, Seed.Fields(tagIds: entry));

        Assert.Equal(EntryChange.Forbidden, await Store(database).RenameAsync(anna, ClassificationKind.Tag, entry, "Energie", Ct));
        Assert.Equal("Strom", Assert.Single(await Store(database).VisibleEntriesAsync(anna, ClassificationKind.Tag, EntryAudience.Account, Ct)).Name);
    }

    [Fact]
    public async Task Rename_IsAllowed_ForAChosenEditor_OfEverySharedDocument()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "store_rename_editor");
        var admin = await database.AddAdminAsync();
        var bob = await database.AddMemberAsync(admin.Id, "bob");
        var db = database.Db();
        var entry = await Seed.EntryAsync(db, ClassificationKind.Correspondent, "Finanzamt", admin.Id);
        var document = await Seed.DocumentAsync(db, admin.Id, isPrivate: false, Seed.Fields(correspondentId: entry));
        document.SetEditors(admin.Id, [bob.Id]);
        await db.SaveChangesAsync(Ct);

        Assert.Equal(EntryChange.Changed, await Store(database).RenameAsync(bob.Id, ClassificationKind.Correspondent, entry, "Finanzamt Mitte", Ct));

        document.MakePrivate(admin.Id);
        await db.SaveChangesAsync(Ct);
        Assert.Equal(EntryChange.NotFound, await Store(database).RenameAsync(bob.Id, ClassificationKind.Correspondent, entry, "Finanzamt", Ct));
    }

    [Fact]
    public async Task Rename_OfAnEntryTheActorMayNotSee_IsNotFound()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "store_rename_hidden");
        var anna = Guid.CreateVersion7();
        var entry = await Seed.EntryAsync(database.Db(), ClassificationKind.DocumentType, "Arztbrief", anna);

        Assert.Equal(EntryChange.NotFound, await Store(database).RenameAsync(Guid.CreateVersion7(), ClassificationKind.DocumentType, entry, "x", Ct));
        Assert.Equal(EntryChange.NotFound, await Store(database).RenameAsync(anna, ClassificationKind.DocumentType, Guid.CreateVersion7(), "x", Ct));
        Assert.Equal(EntryChange.Changed, await Store(database).RenameAsync(anna, ClassificationKind.DocumentType, entry, "Befund", Ct));
    }

    private static ClassificationStore Store(AccountDatabase database) => new(database.Db(), TimeProvider.System);

    private static Domain.Documents.DocumentFields Uses(ClassificationKind kind, Guid entry) => kind switch
    {
        ClassificationKind.Correspondent => Seed.Fields(correspondentId: entry),
        ClassificationKind.DocumentType => Seed.Fields(documentTypeId: entry),
        _ => Seed.Fields(tagIds: entry),
    };
}
