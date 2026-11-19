using NoMorePaper.Application.Accounts;
using NoMorePaper.Application.Classification;
using NoMorePaper.Application.Tests.Accounts;
using NoMorePaper.Domain.Documents;
using Xunit;

namespace NoMorePaper.Application.Tests.Classification;

/// <summary>"Fits" and "hand over" of documents whose owner was unknown on arrival (ADR-0024).</summary>
public sealed class OwnerCheckTests
{
    private static readonly Guid s_member = Guid.CreateVersion7();

    private readonly FakeDocumentFieldsStore _documents = new();
    private readonly FakeCurrentAccount _current = new() { IsAdmin = true };
    private readonly FakeAccountStore _accounts = new();
    private readonly FakeAccountPreferencesStore _preferences = new();
    private readonly RequeueOnlyJobQueue _queue = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Confirm_ByTheOwner_ClearsTheCheck_AndKeepsItPrivate()
    {
        var document = Unknown();
        var log = new RecordingLogger<ConfirmOwnerCheck>();

        Assert.Equal(ChangeOutcome.Changed, await new ConfirmOwnerCheck(_documents, _current, log).ConfirmAsync(document.Id, Ct));

        Assert.Equal((false, true, 1), (document.NeedsOwnerCheck, document.IsPrivate, _documents.Saves));
        Assert.Equal([5261], log.EventIds);
    }

    [Fact]
    public async Task Confirm_IsAConflict_WhenNothingWaits_AndNotFoundForOthers()
    {
        var known = _documents.Add(_current.Id, isPrivate: false);
        var hidden = Unknown();
        var confirm = new ConfirmOwnerCheck(_documents, _current, new RecordingLogger<ConfirmOwnerCheck>());

        Assert.Equal(ChangeOutcome.Conflict, await confirm.ConfirmAsync(known.Id, Ct));

        _current.Id = s_member;
        Assert.Equal(ChangeOutcome.NotFound, await confirm.ConfirmAsync(hidden.Id, Ct));
        Assert.Equal(0, _documents.Saves);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HandOver_GivesItToTheMember_WithTheMembersPreference(bool memberWantsPrivate)
    {
        var document = Unknown();
        _preferences.Saved[s_member] = new AccountPreferences(memberWantsPrivate, []);
        var log = new RecordingLogger<HandOverDocument>();

        Assert.Equal(ChangeOutcome.Changed, await HandOver(log).HandOverAsync(document.Id, s_member, Ct));

        Assert.Equal((s_member, memberWantsPrivate, false), (document.OwnerId, document.IsPrivate, document.NeedsOwnerCheck));
        Assert.Equal([5303], log.EventIds);
    }

    [Fact]
    public async Task HandOver_OfUnconfirmedFields_DropsTheAdminsEntries_AndRecognizesAgain()
    {
        // The administrator's private correspondent must not reach the member through the fields.
        var document = Unknown();
        var adminsPrivateEntry = Guid.CreateVersion7();
        document.Classify(new DocumentFields("Arztbrief Dr. X", adminsPrivateEntry, null, null, null, "Befund.", []));

        await HandOver().HandOverAsync(document.Id, s_member, Ct);

        Assert.Null(document.CorrespondentId);
        Assert.Equal(("Scan", RecognitionState.None), (document.Title, document.Recognition));
        Assert.Equal([document.Id], _queue.Requeued);
    }

    [Fact]
    public async Task HandOver_OfConfirmedFields_RecognizesNothingAgain()
    {
        var document = Unknown();
        document.Confirm(_current.Id);

        await HandOver().HandOverAsync(document.Id, s_member, Ct);

        Assert.Empty(_queue.Requeued);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HandOver_OfConfirmedFields_DropsTheAdminsPrivateEntries_AndKeepsHouseholdOnes(bool memberWantsPrivate)
    {
        // Confirmed fields stay, but the administrator's private entries must not reach the member (ADR-0023).
        var document = Unknown();
        var (adminsSender, householdTag, adminsTag) = (Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        _documents.PrivateEntries[adminsSender] = _current.Id;
        _documents.PrivateEntries[adminsTag] = _current.Id;
        _documents.VisibleEntries.Add(householdTag);
        document.Edit(_current.Id, new DocumentFields("Vertrag", adminsSender, null, null, null, "Kurz.", [householdTag, adminsTag]));
        _preferences.Saved[s_member] = new AccountPreferences(memberWantsPrivate, []);
        var log = new RecordingLogger<HandOverDocument>();

        Assert.Equal(ChangeOutcome.Changed, await HandOver(log).HandOverAsync(document.Id, s_member, Ct));

        Assert.Null(document.CorrespondentId);
        Assert.Equal([householdTag], document.Tags.Select(tag => tag.TagId));
        Assert.Equal(("Vertrag", RecognitionState.Confirmed), (document.Title, document.Recognition));
        Assert.Contains("2 list entries dropped", Assert.Single(log.Messages), StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandOver_IntoAPrivateDocument_KeepsTheNewOwnersOwnPrivateEntry_ButNotIntoAShared()
    {
        var membersEntry = Guid.CreateVersion7();
        _documents.PrivateEntries[membersEntry] = s_member;

        foreach (var (memberWantsPrivate, kept) in new[] { (true, true), (false, false) })
        {
            var document = Unknown();
            document.Edit(_current.Id, new DocumentFields(null, membersEntry, null, null, null, null, []));
            _preferences.Saved[s_member] = new AccountPreferences(memberWantsPrivate, []);

            await HandOver().HandOverAsync(document.Id, s_member, Ct);

            Assert.Equal(kept, document.CorrespondentId == membersEntry);
        }
    }

    [Fact]
    public async Task HandOver_IsRefused_ToNobodyOrSelf_WithoutCheck_AndByOthers()
    {
        var document = Unknown();
        var known = _documents.Add(_current.Id, isPrivate: false);
        var memberShared = _documents.Add(s_member, isPrivate: false);
        var handOver = HandOver();

        Assert.Equal(ChangeOutcome.Invalid, await handOver.HandOverAsync(document.Id, _current.Id, Ct));
        Assert.Equal(ChangeOutcome.Conflict, await handOver.HandOverAsync(known.Id, s_member, Ct));
        Assert.Equal(ChangeOutcome.Forbidden, await handOver.HandOverAsync(memberShared.Id, Guid.CreateVersion7(), Ct));

        _accounts.KnownAccount = false;
        Assert.Equal(ChangeOutcome.Invalid, await handOver.HandOverAsync(document.Id, s_member, Ct));
        Assert.Equal((0, _current.Id), (_documents.Saves, document.OwnerId!.Value));
    }

    private HandOverDocument HandOver(RecordingLogger<HandOverDocument>? log = null) =>
        new(_documents, _accounts, _preferences, _queue, _current, log ?? new RecordingLogger<HandOverDocument>());

    private Document Unknown()
    {
        var document = Document.Receive("Scan", DateTimeOffset.UnixEpoch, _current.Id, isPrivate: false, DocumentSource.ScannerFolder);
        document.AttachFile(new DocumentFile(DocumentFileKind.Original, "Scan.pdf", "application/pdf", 1, new string('0', 64)));
        document.MarkOwnerCheck();
        _documents.Documents.Add(document);
        return document;
    }
}
