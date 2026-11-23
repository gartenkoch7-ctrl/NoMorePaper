using NoMorePaper.Application.Accounts;
using NoMorePaper.Application.Editing;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Documents;
using NoMorePaper.Infrastructure.Editing;
using NoMorePaper.Infrastructure.Inputs;
using NoMorePaper.Infrastructure.Tests.Accounts;
using NoMorePaper.Infrastructure.Tests.Classification;
using NoMorePaper.Infrastructure.Tests.Persistence;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Inputs;

/// <summary>The default which versions an edited document keeps (M4b), next to the other preferences.</summary>
[Collection(SharedPostgres.Name)]
public sealed class AfterEditingStoreTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AfterEditing_IsKeepBoth_UntilChosen_AndSurvivesTheOtherPreferences()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "after_editing");
        var admin = await database.AddAdminAsync();
        Assert.Equal(VersionPolicy.KeepBoth, await Store(database).GetAfterEditingAsync(admin.Id, Ct));

        // A row written before (only the other preferences) gets the default of the column.
        await Store(database).SaveAsync(admin.Id, new AccountPreferences(true, []), Ct);
        Assert.Equal(VersionPolicy.KeepBoth, await Store(database).GetAfterEditingAsync(admin.Id, Ct));

        Assert.True(await Store(database).SetAfterEditingAsync(admin.Id, VersionPolicy.EditedOnly, Ct));
        await Store(database).SaveAsync(admin.Id, new AccountPreferences(false, ["a@x.example"]), Ct);

        Assert.Equal(VersionPolicy.EditedOnly, await Store(database).GetAfterEditingAsync(admin.Id, Ct));
        Assert.Equal(["a@x.example"], (await Store(database).GetAsync(admin.Id, Ct)).MailSenders);
    }

    [Fact]
    public async Task AfterEditing_OfAnAccountWithoutPreferences_KeepsNewDocumentsShared()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "after_editing_new");
        var admin = await database.AddAdminAsync();

        Assert.True(await Store(database).SetAfterEditingAsync(admin.Id, VersionPolicy.OriginalOnly, Ct));

        var preferences = await Store(database).GetAsync(admin.Id, Ct);
        Assert.False(preferences.NewDocumentsPrivate);
        Assert.Empty(preferences.MailSenders);
        Assert.False(await Store(database).SetAfterEditingAsync(Guid.CreateVersion7(), VersionPolicy.OriginalOnly, Ct));
    }

    [Fact]
    public async Task TheDetail_ShowsTheDocumentsChoice_ElseTheOwnersDefault()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "after_editing_detail");
        var admin = await database.AddAdminAsync();
        var db = database.Db();
        var plain = await Seed.DocumentAsync(db, admin.Id, isPrivate: false, text: "Brief");
        var chosen = await Seed.DocumentAsync(db, admin.Id, isPrivate: false, text: "Brief");
        chosen.ChooseVersions(admin.Id, VersionPolicy.KeepBoth);
        await db.SaveChangesAsync(Ct);
        await Store(database).SetAfterEditingAsync(admin.Id, VersionPolicy.EditedOnly, Ct);
        await new EfDocumentEdits(db).SaveAsync(new DocumentEdit(chosen.Id, EditKind.Rebuild, "{}", admin.Id, DateTimeOffset.UtcNow, false), Ct);
        var viewer = Guid.CreateVersion7();

        var repository = new EfDocumentRepository(database.Db());
        Assert.Equal(VersionPolicy.EditedOnly, (await repository.GetAsync(plain.Id, viewer, Ct))?.VersionPolicy);
        Assert.Equal(VersionPolicy.KeepBoth, (await repository.GetAsync(chosen.Id, viewer, Ct))?.VersionPolicy);
        Assert.Equal((false, true), ((await repository.GetAsync(plain.Id, viewer, Ct))!.EditPending, (await repository.GetAsync(chosen.Id, viewer, Ct))!.EditPending));
    }

    private static EfAccountPreferencesStore Store(AccountDatabase database) => new(database.Db());
}
