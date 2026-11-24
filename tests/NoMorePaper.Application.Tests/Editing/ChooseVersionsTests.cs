using NoMorePaper.Application.Classification;
using NoMorePaper.Application.Editing;
using NoMorePaper.Application.Tests.Accounts;
using NoMorePaper.Application.Tests.Classification;
using NoMorePaper.Application.Tests.Documents;
using NoMorePaper.Application.Tests.Trash;
using NoMorePaper.Application.Trash;
using NoMorePaper.Domain.Documents;
using Xunit;
using static NoMorePaper.Application.Tests.Editing.EditSamples;

namespace NoMorePaper.Application.Tests.Editing;

/// <summary>The owner's choice of versions for one document ("Fassungen", ADR-0025).</summary>
public sealed class ChooseVersionsTests
{
    private readonly FakeDocumentFieldsStore _documents = new();
    private readonly FakeTrashStore _trash = new();
    private readonly FakeDocumentEdits _edits = new();
    private readonly RequeueOnlyJobQueue _queue = new();
    private readonly FakeDocumentStorage _storage = new();
    private readonly FakeCurrentAccount _current = new();
    private readonly RecordingLogger<ChooseVersions> _log = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task OnlyEdited_MovesTheOriginalToTheTrash()
    {
        var document = _documents.Add(_current.Id, isPrivate: false).Done(edited: true);

        Assert.Equal(ChangeOutcome.Changed, await Choose().ChooseAsync(document.Id, VersionPolicy.EditedOnly, Ct));

        Assert.Null(document.FindFile(DocumentFileKind.Original));
        Assert.Equal(VersionPolicy.EditedOnly, document.VersionPolicy);
        var item = Assert.Single(_trash.Items);
        Assert.Equal(TrashItemKind.Original, item.Kind);
        Assert.Equal([(document.Id, DocumentFileKind.Original, item.Id)], _storage.Trashed);
        Assert.Empty(_queue.EditsQueued);
        Assert.Contains(5503, _log.EventIds);
    }

    [Fact]
    public async Task OnlyOriginal_MovesTheEditedVersionToTheTrash_AndQueuesANewArchive()
    {
        var document = _documents.Add(_current.Id, isPrivate: false).Done(edited: true);

        Assert.Equal(ChangeOutcome.Changed, await Choose().ChooseAsync(document.Id, VersionPolicy.OriginalOnly, Ct));

        Assert.Null(document.FindFile(DocumentFileKind.Edited));
        Assert.Equal(TrashItemKind.Edited, Assert.Single(_trash.Items).Kind);
        Assert.Equal(EditKind.Rebuild, _edits.Edits[document.Id].Kind);
        Assert.Equal([document.Id], _queue.EditsQueued);
        Assert.Contains(5515, _log.EventIds);
    }

    [Fact]
    public async Task WithoutEditedVersion_OnlyTheChoiceIsKept_ForTheNextEdit()
    {
        var document = _documents.Add(_current.Id, isPrivate: false).Done();

        Assert.Equal(ChangeOutcome.Changed, await Choose().ChooseAsync(document.Id, VersionPolicy.OriginalOnly, Ct));

        Assert.Equal(VersionPolicy.OriginalOnly, document.VersionPolicy);
        Assert.Empty(_trash.Items);
        Assert.Empty(_queue.EditsQueued);
    }

    [Fact]
    public async Task AFileThatCannotMove_StaysWhereItIs_AndTheChoiceHolds()
    {
        var document = _documents.Add(_current.Id, isPrivate: false).Done(edited: true);
        _storage.TrashFailure = new IOException("disk");

        Assert.Equal(ChangeOutcome.Changed, await Choose().ChooseAsync(document.Id, VersionPolicy.EditedOnly, Ct));
        Assert.Contains(5504, _log.EventIds);
    }

    [Fact]
    public async Task Refusals_LeaveTheDocumentAsItWas()
    {
        var owner = Guid.CreateVersion7();
        var editorOnly = _documents.Add(owner, isPrivate: false).Done(edited: true);
        editorOnly.SetEditors(owner, [_current.Id]);
        var pending = _documents.Add(_current.Id, isPrivate: false).Done(edited: true);
        await _edits.SaveAsync(new DocumentEdit(pending.Id, EditKind.Pages, "{}", _current.Id, Now, false), Ct);
        var busy = _documents.Add(_current.Id, isPrivate: false).Done(edited: true);

        Assert.Equal(ChangeOutcome.Forbidden, await Choose().ChooseAsync(editorOnly.Id, VersionPolicy.OriginalOnly, Ct));
        Assert.Equal(ChangeOutcome.Conflict, await Choose().ChooseAsync(pending.Id, VersionPolicy.OriginalOnly, Ct));
        Assert.Equal(ChangeOutcome.Invalid, await Choose().ChooseAsync(busy.Id, (VersionPolicy)9, Ct));
        _queue.EnqueueEditResult = false;
        Assert.Equal(ChangeOutcome.Conflict, await Choose().ChooseAsync(busy.Id, VersionPolicy.OriginalOnly, Ct));

        Assert.NotNull(busy.FindFile(DocumentFileKind.Edited));
        Assert.Null(busy.VersionPolicy);
        Assert.Empty(_trash.Items);
        Assert.Equal(0, _documents.Saves);
    }

    private ChooseVersions Choose() => new(_documents, _trash, _edits, _queue, _storage, _current, new FixedClock(Now), _log);
}
