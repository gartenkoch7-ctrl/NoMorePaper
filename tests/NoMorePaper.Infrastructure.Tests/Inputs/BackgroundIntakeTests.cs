using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Application.Accounts;
using NoMorePaper.Application.Documents;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Classification;
using NoMorePaper.Infrastructure.Documents;
using NoMorePaper.Infrastructure.Inputs;
using NoMorePaper.Infrastructure.Tests.Accounts;
using NoMorePaper.Infrastructure.Tests.Classification;
using NoMorePaper.Infrastructure.Tests.Documents;
using NoMorePaper.Infrastructure.Tests.Persistence;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Inputs;

/// <summary>The intake of the background inputs against Postgres: owner, privacy, owner check, duplicates, review.</summary>
[Collection(SharedPostgres.Name)]
public sealed class BackgroundIntakeTests(PostgresFixture postgres) : IDisposable
{
    private readonly DirectoryInfo _sandbox = Directory.CreateTempSubdirectory("nomorepaper-inputs-");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _sandbox.Delete(recursive: true);

    [Fact]
    public async Task AnUnknownSender_GoesToTheAdministrator_Privately_WithItsMailOrigin_IntoItsReview()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "inputs_unknown");
        var admin = await database.AddAdminAsync();
        var bob = await database.AddMemberAsync(admin.Id, "bob");
        var origin = new MailOrigin("info@versicherung.example", "Ihr Vertrag", "Kündigung bis 31.12. möglich.");

        var result = await Intake(database).AcceptAsync(Request(null, origin), null, DuplicateScope.Owned, Ct);

        var fields = new EfDocumentFieldsStore(database.Db());
        var view = await fields.GetFieldsAsync(result.DocumentId!.Value, admin.Id, Ct);
        Assert.Equal((DocumentSource.Mail, origin, true), (view!.Source, view.Mail, view.NeedsOwnerCheck));
        Assert.Null(await fields.GetFieldsAsync(result.DocumentId.Value, bob.Id, Ct));
        Assert.Equal([result.DocumentId.Value], (await fields.ListReviewAsync(admin.Id, Ct)).Select(item => item.Id));
        Assert.Equal(1, await fields.CountReviewAsync(admin.Id, Ct));
        Assert.Equal(0, await fields.CountReviewAsync(bob.Id, Ct));
    }

    [Fact]
    public async Task AKnownOwner_GetsItWithTheirPreference_AndOnlyTheirOwnFileIsADuplicate()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "inputs_known");
        var admin = await database.AddAdminAsync();
        var bob = await database.AddMemberAsync(admin.Id, "bob");
        await new EfAccountPreferencesStore(database.Db()).SaveAsync(bob.Id, new AccountPreferences(true, []), Ct);

        // The administrator's shared document with the same bytes does not stop Bob's copy.
        var shared = await Intake(database).AcceptAsync(Request(admin.Id), isPrivate: false, DuplicateScope.Owned, Ct);
        var bobs = await Intake(database).AcceptAsync(Request(bob.Id), null, DuplicateScope.Owned, Ct);
        var again = await Intake(database).AcceptAsync(Request(bob.Id), null, DuplicateScope.Owned, Ct);

        Assert.Equal((IntakeStatus.Received, IntakeStatus.Received), (shared.Status, bobs.Status));
        Assert.Equal(IntakeResult.AlreadyThere(bobs.DocumentId!.Value), again);
        var document = await database.Db().Documents.AsNoTracking().SingleAsync(found => found.Id == bobs.DocumentId, Ct);
        Assert.Equal((bob.Id, true, false, DocumentSource.ScannerFolder), (document.OwnerId!.Value, document.IsPrivate, document.NeedsOwnerCheck, document.Source));
        Assert.Equal(2, await database.Db().Documents.CountAsync(Ct));
    }

    [Fact]
    public async Task ARemovedOwner_AndAHouseholdWithoutAdministrator_TakeNothing()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "inputs_nobody");

        Assert.Equal(IntakeResult.NoAdministrator, await Intake(database).AcceptAsync(Request(null), null, DuplicateScope.Owned, Ct));
        Assert.Equal(IntakeResult.OwnerGone, await Intake(database).AcceptAsync(Request(Guid.CreateVersion7()), null, DuplicateScope.Owned, Ct));
        Assert.Equal(0, await database.Db().Documents.CountAsync(Ct));
    }

    [Fact]
    public async Task TheAdministrator_IsTheOldestAdministrator()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "inputs_admin");
        Assert.Null(await database.Store().FindAdministratorIdAsync(Ct));

        var admin = await database.AddAdminAsync();
        await database.AddMemberAsync(admin.Id, "bob");

        Assert.Equal(admin.Id, await database.Store().FindAdministratorIdAsync(Ct));
    }

    [Fact]
    public async Task FindOwned_IgnoresOtherAccountsDocuments()
    {
        await using var database = await AccountDatabase.CreateAsync(postgres, "inputs_owned");
        var sha = new string('c', 64);
        var anna = Guid.CreateVersion7();
        var first = await Seed.DocumentAsync(database.Db(), anna, isPrivate: false, sha256: sha);
        await Seed.DocumentAsync(database.Db(), anna, isPrivate: true, sha256: sha);
        var repository = new EfDocumentRepository(database.Db());

        Assert.Equal(first.Id, await repository.FindOwnedBySha256Async(anna, sha, Ct));
        Assert.Null(await repository.FindOwnedBySha256Async(Guid.CreateVersion7(), sha, Ct));
        Assert.Null(await repository.FindOwnedBySha256Async(anna, new string('d', 64), Ct));
    }

    private IntakeDocument Intake(AccountDatabase database)
    {
        var db = database.Db();
        return new IntakeDocument(
            new EfDocumentRepository(db),
            new FileSystemDocumentStorage(_sandbox.FullName, new RecordingDirectorySync(), NullLogger<FileSystemDocumentStorage>.Instance),
            database.Store(),
            new EfAccountPreferencesStore(db),
            TimeProvider.System,
            NullLogger<IntakeDocument>.Instance);
    }

    private static IntakeRequest Request(Guid? ownerId, MailOrigin? mail = null) =>
        new(ownerId, mail is null ? DocumentSource.ScannerFolder : DocumentSource.Mail, "Scan 1.pdf", "application/pdf", new MemoryStream([1, 2, 3, 4]), mail);
}
