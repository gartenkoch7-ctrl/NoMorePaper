using Microsoft.EntityFrameworkCore;
using NoMorePaper.Domain.Classification;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Classification;
using NoMorePaper.Infrastructure.Tests.Accounts;
using NoMorePaper.Infrastructure.Tests.Persistence;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Classification;

/// <summary>Fields and "to review" against Postgres: the viewer never learns of another account's private document.</summary>
[Collection(SharedPostgres.Name)]
public sealed class DocumentFieldsStoreTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Fields_CarryNamesTagsEditorsAndRights()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "fields_view");
        var admin = await database.AddAdminAsync();
        var bob = await database.AddMemberAsync(admin.Id, "bob");
        var db = database.Db();
        var sender = await Seed.EntryAsync(db, ClassificationKind.Correspondent, "Stadtwerke", admin.Id);
        var strom = await Seed.EntryAsync(db, ClassificationKind.Tag, "Strom", admin.Id);
        var abschlag = await Seed.EntryAsync(db, ClassificationKind.Tag, "Abschlag", admin.Id);
        var document = await Seed.DocumentAsync(
            db, admin.Id, isPrivate: false, Seed.Fields(sender, null, new DateOnly(2026, 11, 1), new Money(84.2m, "EUR"), strom, abschlag));
        document.SetEditors(admin.Id, [bob.Id]);
        await db.SaveChangesAsync(Ct);

        var forBob = await Store(database).GetFieldsAsync(document.Id, bob.Id, Ct);
        var forOther = await Store(database).GetFieldsAsync(document.Id, Guid.CreateVersion7(), Ct);

        Assert.NotNull(forBob);
        Assert.Equal(("Stadtwerke", new Money(84.2m, "EUR"), RecognitionState.Automatic), (forBob.Correspondent?.Name, forBob.Amount, forBob.Recognition));
        Assert.Equal(["Abschlag", "Strom"], forBob.Tags.Select(tag => tag.Name));
        Assert.Equal([bob.Id], forBob.EditorIds);
        Assert.True(forBob.CanEdit);
        Assert.False(forOther!.CanEdit);
    }

    [Fact]
    public async Task Fields_OfAnotherAccountsPrivateDocument_DoNotExist()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "fields_private");
        var anna = Guid.CreateVersion7();
        var document = await Seed.DocumentAsync(database.Db(), anna, isPrivate: true);

        Assert.Null(await Store(database).GetFieldsAsync(document.Id, Guid.CreateVersion7(), Ct));
        Assert.Null(await Store(database).FindVisibleAsync(document.Id, Guid.CreateVersion7(), Ct));
        Assert.NotNull(await Store(database).GetFieldsAsync(document.Id, anna, Ct));
    }

    [Fact]
    public async Task TheDuplicateHint_IsHidden_WhenTheViewerMayNotSeeTheOtherDocument()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "fields_duplicate");
        var anna = Guid.CreateVersion7();
        var bob = Guid.CreateVersion7();
        var db = database.Db();
        var annasPrivate = await Seed.DocumentAsync(db, anna, isPrivate: true);
        var shared = await Seed.DocumentAsync(db, anna, isPrivate: false);
        shared.MarkPossibleDuplicate(annasPrivate.Id);
        await db.SaveChangesAsync(Ct);

        Assert.Equal(annasPrivate.Id, (await Store(database).GetFieldsAsync(shared.Id, anna, Ct))!.PossibleDuplicateOf);
        Assert.Null((await Store(database).GetFieldsAsync(shared.Id, bob, Ct))!.PossibleDuplicateOf);
    }

    [Fact]
    public async Task Review_ListsEditableDocuments_RecognizedAutomaticallyOrVisiblyDuplicated_NewestFirst()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "fields_review");
        var admin = await database.AddAdminAsync();
        var bob = await database.AddMemberAsync(admin.Id, "bob");
        var db = database.Db();
        var sender = await Seed.EntryAsync(db, ClassificationKind.Correspondent, "Finanzamt", admin.Id);
        var original = await Seed.DocumentAsync(db, bob.Id, isPrivate: false, Seed.Fields(correspondentId: sender));
        var automatic = await Seed.DocumentAsync(db, bob.Id, isPrivate: true, Seed.Fields());
        var confirmed = await Seed.DocumentAsync(db, bob.Id, isPrivate: false, Seed.Fields());
        confirmed.Confirm(bob.Id);
        var duplicate = await Seed.DocumentAsync(db, bob.Id, isPrivate: false);
        duplicate.MarkPossibleDuplicate(original.Id);
        var hiddenDuplicate = await Seed.DocumentAsync(db, bob.Id, isPrivate: false);
        hiddenDuplicate.MarkPossibleDuplicate((await Seed.DocumentAsync(db, admin.Id, isPrivate: true)).Id);
        var edited = await Seed.DocumentAsync(db, admin.Id, isPrivate: false, Seed.Fields());
        edited.SetEditors(admin.Id, [bob.Id]);
        var privateEdited = await Seed.DocumentAsync(db, admin.Id, isPrivate: true, Seed.Fields());
        privateEdited.SetEditors(admin.Id, [bob.Id]);
        await Seed.DocumentAsync(db, admin.Id, isPrivate: false, Seed.Fields());
        await db.SaveChangesAsync(Ct);

        var review = await Store(database).ListReviewAsync(bob.Id, Ct);

        Assert.Equal([edited.Id, duplicate.Id, automatic.Id, original.Id], review.Select(item => item.Id));
        var candidate = review.Single(item => item.Id == duplicate.Id).PossibleDuplicate;
        Assert.Equal((original.Id, "Finanzamt"), (candidate?.Id, candidate?.Correspondent?.Name));
        Assert.All(review, item => Assert.True(item.Fields.CanEdit));
    }

    [Fact]
    public async Task Delete_RemovesTheDocumentWithItsJob_AndClearsHintsPointingToIt()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "fields_delete");
        var anna = Guid.CreateVersion7();
        var db = database.Db();
        var copy = await Seed.DocumentAsync(db, anna, isPrivate: false);
        var other = await Seed.DocumentAsync(db, anna, isPrivate: false);
        other.MarkPossibleDuplicate(copy.Id);
        await db.SaveChangesAsync(Ct);
        await db.Database.ExecuteSqlAsync($"INSERT INTO processing_jobs (document_id, attempt, next_attempt_at) VALUES ({copy.Id}, 0, now())", Ct);

        var store = Store(database);
        await store.DeleteAsync((await store.FindVisibleAsync(copy.Id, anna, Ct))!, Ct);

        var read = database.Db();
        Assert.False(await read.Documents.AnyAsync(d => d.Id == copy.Id, Ct));
        Assert.False(await read.ProcessingJobs.AnyAsync(j => j.DocumentId == copy.Id, Ct));
        Assert.Null((await read.Documents.SingleAsync(d => d.Id == other.Id, Ct)).PossibleDuplicateOf);
    }

    private static EfDocumentFieldsStore Store(AccountDatabase database) => new(database.Db());
}
