using NoMorePaper.Application.Accounts;
using NoMorePaper.Application.Classification;
using NoMorePaper.Application.Tests.Accounts;
using NoMorePaper.Application.Tests.Documents;
using NoMorePaper.Domain.Documents;
using Xunit;

namespace NoMorePaper.Application.Tests.Classification;

/// <summary>Confirming, editors, duplicates and the reads: who may, and what changes.</summary>
public sealed class DocumentCommandTests
{
    private readonly FakeDocumentFieldsStore _documents = new();
    private readonly FakeCurrentAccount _current = new();
    private readonly FakeAccountStore _accounts = new();
    private readonly FakeDocumentStorage _storage = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Confirm_ByAnEditor_ConfirmsTheFields_AndOthersAreForbidden()
    {
        var mine = _documents.Add(_current.Id, isPrivate: false);
        var theirs = _documents.Add(Guid.CreateVersion7(), isPrivate: false);
        var log = new RecordingLogger<ConfirmDocument>();
        var confirm = new ConfirmDocument(_documents, _current, log);

        Assert.Equal(ChangeOutcome.Changed, await confirm.ConfirmAsync(mine.Id, RecognitionState.None, Ct));
        Assert.Equal(ChangeOutcome.Forbidden, await confirm.ConfirmAsync(theirs.Id, RecognitionState.None, Ct));
        Assert.Equal(ChangeOutcome.NotFound, await confirm.ConfirmAsync(Guid.CreateVersion7(), RecognitionState.None, Ct));

        Assert.Equal((RecognitionState.Confirmed, RecognitionState.None), (mine.Recognition, theirs.Recognition));
        Assert.Equal([5251, 5256, 5256], log.EventIds);
    }

    [Fact]
    public async Task Confirm_WithAnOutdatedState_IsAConflict_AndConfirmsNothing()
    {
        var document = _documents.Add(_current.Id, isPrivate: false);
        document.Classify(new DocumentFields("Erkannt", null, null, null, null, "Kurz.", []));
        var log = new RecordingLogger<ConfirmDocument>();

        // The page still showed the document before the recognition finished.
        Assert.Equal(ChangeOutcome.Conflict, await new ConfirmDocument(_documents, _current, log).ConfirmAsync(document.Id, RecognitionState.None, Ct));

        Assert.Equal((0, RecognitionState.Automatic), (_documents.Saves, document.Recognition));
        Assert.Equal([5256], log.EventIds);
    }

    [Fact]
    public async Task KeepDuplicate_RemovesTheHint_ForEditorsOnly()
    {
        var mine = Duplicate(_current.Id);
        var theirs = Duplicate(Guid.CreateVersion7());
        var keep = new KeepDuplicate(_documents, _current, new RecordingLogger<KeepDuplicate>());

        Assert.Equal(ChangeOutcome.Changed, await keep.KeepAsync(mine.Id, Ct));
        Assert.Equal(ChangeOutcome.Forbidden, await keep.KeepAsync(theirs.Id, Ct));

        Assert.Null(mine.PossibleDuplicateOf);
        Assert.NotNull(theirs.PossibleDuplicateOf);
        Assert.Equal(1, _documents.Saves);
    }

    [Fact]
    public async Task DeleteDuplicate_ByTheOwner_DeletesRecordAndFiles()
    {
        var document = Duplicate(_current.Id);
        var log = new RecordingLogger<DeleteDuplicate>();

        Assert.Equal(ChangeOutcome.Changed, await Delete(log).DeleteAsync(document.Id, Ct));

        Assert.Equal([document.Id], _documents.Deleted);
        Assert.Equal([document.Id], _storage.Deleted);
        Assert.Equal([5254], log.EventIds);
    }

    [Fact]
    public async Task DeleteDuplicate_IsRefused_WithoutHint_OrForAnEditorWhoIsNotTheOwner()
    {
        var plain = _documents.Add(_current.Id, isPrivate: false);
        var owner = Guid.CreateVersion7();
        var edited = Duplicate(owner);
        edited.SetEditors(owner, [_current.Id]);

        Assert.Equal(ChangeOutcome.Conflict, await Delete().DeleteAsync(plain.Id, Ct));
        Assert.Equal(ChangeOutcome.Forbidden, await Delete().DeleteAsync(edited.Id, Ct));

        Assert.Empty(_documents.Deleted);
        Assert.Empty(_storage.Deleted);
    }

    [Fact]
    public async Task DeleteDuplicate_WhenTheFilesCannotBeRemoved_StillReportsTheDeletion()
    {
        var document = Duplicate(_current.Id);
        _storage.DeleteFailure = new IOException("busy");
        var log = new RecordingLogger<DeleteDuplicate>();

        Assert.Equal(ChangeOutcome.Changed, await Delete(log).DeleteAsync(document.Id, Ct));
        Assert.Equal([5254, 5255], log.EventIds);
    }

    [Fact]
    public async Task SetEditors_ByTheOwner_TakesMembersOnly()
    {
        var member = new Account(Guid.CreateVersion7(), "bob", "Bob", false, "stamp");
        _accounts.Members.Add(member);
        var document = _documents.Add(_current.Id, isPrivate: false);
        var set = new SetDocumentEditors(_documents, _accounts, _current, new RecordingLogger<SetDocumentEditors>());

        Assert.Equal(ChangeOutcome.Changed, await set.SetAsync(document.Id, [member.Id], Ct));
        Assert.Equal(ChangeOutcome.Invalid, await set.SetAsync(document.Id, [member.Id, Guid.CreateVersion7()], Ct));
        Assert.Equal(ChangeOutcome.Invalid, await set.SetAsync(document.Id, null, Ct));

        Assert.Equal(member.Id, Assert.Single(document.Editors).AccountId);
    }

    [Fact]
    public async Task SetEditors_IsForbidden_ForAnEditorWhoIsNotTheOwner()
    {
        var owner = Guid.CreateVersion7();
        var document = _documents.Add(owner, isPrivate: false);
        document.SetEditors(owner, [_current.Id]);
        var set = new SetDocumentEditors(_documents, _accounts, _current, new RecordingLogger<SetDocumentEditors>());

        Assert.Equal(ChangeOutcome.Forbidden, await set.SetAsync(document.Id, [], Ct));
        Assert.Single(document.Editors);
    }

    [Fact]
    public async Task ARemovedAccount_ChangesNothing()
    {
        var document = Duplicate(_current.Id);
        _current.Exists = false;

        Assert.Equal(ChangeOutcome.AccountGone, await new ConfirmDocument(_documents, _current, new RecordingLogger<ConfirmDocument>()).ConfirmAsync(document.Id, RecognitionState.None, Ct));
        Assert.Equal(ChangeOutcome.AccountGone, await new KeepDuplicate(_documents, _current, new RecordingLogger<KeepDuplicate>()).KeepAsync(document.Id, Ct));
        Assert.Equal(ChangeOutcome.AccountGone, await Delete().DeleteAsync(document.Id, Ct));
        Assert.Equal(
            ChangeOutcome.AccountGone,
            await new SetDocumentEditors(_documents, _accounts, _current, new RecordingLogger<SetDocumentEditors>()).SetAsync(document.Id, [], Ct));

        Assert.Equal((0, RecognitionState.None), (_documents.Saves, document.Recognition));
        Assert.NotNull(document.PossibleDuplicateOf);
    }

    [Fact]
    public async Task Reads_AskAsTheSignedInAccount()
    {
        await new GetDocumentFields(_documents, _current).GetAsync(Guid.CreateVersion7(), Ct);
        Assert.Equal(_current.Id, _documents.LastViewer);

        _current.Id = Guid.CreateVersion7();
        await new ListReview(_documents, _current).ListAsync(Ct);
        Assert.Equal(_current.Id, _documents.LastViewer);

        _current.Id = Guid.CreateVersion7();
        _documents.ReviewCount = 4;
        Assert.Equal(4, await new ListReview(_documents, _current).CountAsync(Ct));
        Assert.Equal(_current.Id, _documents.LastViewer);
    }

    private Document Duplicate(Guid owner)
    {
        var document = _documents.Add(owner, isPrivate: false);
        document.MarkPossibleDuplicate(Guid.CreateVersion7());
        return document;
    }

    private DeleteDuplicate Delete(RecordingLogger<DeleteDuplicate>? log = null) =>
        new(_documents, _storage, _current, log ?? new RecordingLogger<DeleteDuplicate>());
}
