using NoMorePaper.Application.Accounts;
using NoMorePaper.Application.Documents;
using NoMorePaper.Application.Tests.Accounts;
using NoMorePaper.Domain.Documents;
using Xunit;

namespace NoMorePaper.Application.Tests.Documents;

/// <summary>The intake of the background inputs: owner, privacy, owner check and duplicates (ADR-0024).</summary>
public sealed class IntakeDocumentTests
{
    private static readonly Guid s_owner = Guid.CreateVersion7();

    private readonly FakeDocumentRepository _documents = new();
    private readonly FakeDocumentStorage _storage = new();
    private readonly FakeAccountStore _accounts = new();
    private readonly FakeAccountPreferencesStore _preferences = new();
    private readonly RecordingLogger<IntakeDocument> _log = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AKnownOwner_GetsItWithTheirPreference(bool newDocumentsPrivate)
    {
        _preferences.Saved[s_owner] = new AccountPreferences(newDocumentsPrivate, []);

        var result = await AcceptAsync(s_owner, isPrivate: null);

        var document = Assert.Single(_documents.Added);
        Assert.Equal(IntakeResult.Received(document.Id), result);
        Assert.Equal((s_owner, newDocumentsPrivate, false, DocumentSource.ScannerFolder), (document.OwnerId, document.IsPrivate, document.NeedsOwnerCheck, document.Source));
        Assert.Equal([3200], _log.EventIds);
    }

    [Fact]
    public async Task TheBrowsersChoice_WinsOverThePreference()
    {
        _preferences.Saved[s_owner] = new AccountPreferences(true, []);

        await AcceptAsync(s_owner, isPrivate: false);

        Assert.False(Assert.Single(_documents.Added).IsPrivate);
    }

    [Fact]
    public async Task AnUnknownOwner_GivesItToTheAdministrator_Privately_ForTheOwnerCheck()
    {
        var admin = _accounts.AdministratorId!.Value;
        _preferences.Saved[admin] = new AccountPreferences(false, []);

        await AcceptAsync(ownerId: null, isPrivate: null);

        var document = Assert.Single(_documents.Added);
        Assert.Equal((admin, true, true), (document.OwnerId, document.IsPrivate, document.NeedsOwnerCheck));
    }

    [Fact]
    public async Task ARemovedOwner_StoresNothing()
    {
        _accounts.KnownAccount = false;

        Assert.Equal(IntakeResult.OwnerGone, await AcceptAsync(s_owner, isPrivate: null));

        Assert.Empty(_documents.Added);
        Assert.Empty(_storage.Inbox);
        Assert.Equal([5301], _log.EventIds);
    }

    [Fact]
    public async Task WithoutAdministrator_AnUnknownOwnersFile_IsNotTaken()
    {
        _accounts.AdministratorId = null;

        Assert.Equal(IntakeResult.NoAdministrator, await AcceptAsync(ownerId: null, isPrivate: null));

        Assert.Empty(_documents.Added);
        Assert.Equal([5302], _log.EventIds);
    }

    [Fact]
    public async Task InTheBackground_OnlyTheOwnersOwnDocumentsAreDuplicates()
    {
        var existing = Guid.CreateVersion7();
        _documents.VisibleDuplicate = Guid.CreateVersion7();

        // Another account's shared document with the same file does not count.
        Assert.Equal(IntakeStatus.Received, (await AcceptAsync(s_owner, isPrivate: null)).Status);
        Assert.Equal((s_owner, FakeDocumentStorage.Sha256), _documents.LastOwnedLookup);
        Assert.Null(_documents.LastHashLookup);

        _documents.OwnedDuplicate = existing;
        Assert.Equal(IntakeResult.AlreadyThere(existing), await AcceptAsync(s_owner, isPrivate: null));
        Assert.Single(_documents.Added);
        Assert.Equal([3200, 5300], _log.EventIds);
    }

    [Fact]
    public async Task InTheBrowser_EveryVisibleDocumentIsADuplicate()
    {
        var existing = Guid.CreateVersion7();
        _documents.VisibleDuplicate = existing;

        var result = await Intake().AcceptAsync(Request(s_owner), isPrivate: false, DuplicateScope.Visible, Ct);

        Assert.Equal(IntakeResult.AlreadyThere(existing), result);
        Assert.Single(_storage.Discarded);
        Assert.Equal([5280], _log.EventIds);
    }

    [Fact]
    public async Task AMail_KeepsItsOrigin()
    {
        var origin = new MailOrigin("a@b.example", "Rechnung", "Zahlbar bis 30.11.");

        await Intake().AcceptAsync(Request(s_owner) with { Source = DocumentSource.Mail, Mail = origin }, null, DuplicateScope.Owned, Ct);

        var document = Assert.Single(_documents.Added);
        Assert.Equal((DocumentSource.Mail, origin), (document.Source, document.MailOrigin));
    }

    private Task<IntakeResult> AcceptAsync(Guid? ownerId, bool? isPrivate) =>
        Intake().AcceptAsync(Request(ownerId), isPrivate, DuplicateScope.Owned, Ct);

    private static IntakeRequest Request(Guid? ownerId) =>
        new(ownerId, DocumentSource.ScannerFolder, "Scan 1.pdf", "application/pdf", new MemoryStream([1, 2, 3]));

    private IntakeDocument Intake() => new(_documents, _storage, _accounts, _preferences, TimeProvider.System, _log);
}
