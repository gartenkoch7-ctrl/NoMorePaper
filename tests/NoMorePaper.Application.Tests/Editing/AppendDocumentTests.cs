using NoMorePaper.Application.Classification;
using NoMorePaper.Application.Editing;
using NoMorePaper.Application.Tests.Accounts;
using NoMorePaper.Application.Tests.Classification;
using NoMorePaper.Application.Tests.Trash;
using Xunit;
using static NoMorePaper.Application.Tests.Editing.EditSamples;

namespace NoMorePaper.Application.Tests.Editing;

/// <summary>"Dokument anhängen" (ADR-0025): rights, state, and the privacy rule.</summary>
public sealed class AppendDocumentTests
{
    private readonly FakeDocumentFieldsStore _documents = new();
    private readonly FakeDocumentEdits _edits = new();
    private readonly RequeueOnlyJobQueue _queue = new();
    private readonly FakeCurrentAccount _current = new();
    private readonly RecordingLogger<AppendDocument> _log = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task APrivateDocument_AppendedToASharedOne_MakesTheResultPrivate_AtOnce()
    {
        var target = _documents.Add(_current.Id, isPrivate: false).Done();
        var appended = _documents.Add(_current.Id, isPrivate: true).Done();

        Assert.Equal(ChangeOutcome.Changed, await Append().AppendAsync(target.Id, appended.Id, Ct));

        Assert.True(target.IsPrivate);
        var edit = _edits.Edits[target.Id];
        Assert.Equal((EditKind.Append, appended.Id), (edit.Kind, EditDataJson.Parse(edit.Data, EditKind.Append)!.AppendedId));
        Assert.Equal(1, _documents.Saves);
    }

    [Fact]
    public async Task AnEditor_MayAppend_ButNotMakeTheOwnersDocumentPrivate()
    {
        var owner = Guid.CreateVersion7();
        var target = _documents.Add(owner, isPrivate: false).Done();
        target.SetEditors(owner, [_current.Id]);
        var shared = _documents.Add(_current.Id, isPrivate: false).Done();
        var secret = _documents.Add(_current.Id, isPrivate: true).Done();

        Assert.Equal(ChangeOutcome.Forbidden, await Append().AppendAsync(target.Id, secret.Id, Ct));
        Assert.False(target.IsPrivate);
        Assert.Equal(ChangeOutcome.Changed, await Append().AppendAsync(target.Id, shared.Id, Ct));
    }

    [Fact]
    public async Task Refusals_FollowTheAppendRule_AndLeaveTheDocumentAsItWas()
    {
        var target = _documents.Add(_current.Id, isPrivate: false).Done();
        var queued = _documents.Add(_current.Id, isPrivate: false);
        var foreign = _documents.Add(Guid.CreateVersion7(), isPrivate: false).Done();
        var hidden = _documents.Add(Guid.CreateVersion7(), isPrivate: true).Done();
        var busy = _documents.Add(_current.Id, isPrivate: true).Done();
        await _edits.SaveAsync(new DocumentEdit(busy.Id, EditKind.Pages, "{}", _current.Id, Now, false), Ct);

        Assert.Equal(ChangeOutcome.Invalid, await Append().AppendAsync(target.Id, target.Id, Ct));
        Assert.Equal(ChangeOutcome.Conflict, await Append().AppendAsync(target.Id, queued.Id, Ct));
        Assert.Equal(ChangeOutcome.Forbidden, await Append().AppendAsync(target.Id, foreign.Id, Ct));
        Assert.Equal(ChangeOutcome.NotFound, await Append().AppendAsync(target.Id, hidden.Id, Ct));
        Assert.Equal(ChangeOutcome.Conflict, await Append().AppendAsync(target.Id, busy.Id, Ct));

        _queue.EnqueueEditResult = false;
        var secret = _documents.Add(_current.Id, isPrivate: true).Done();
        Assert.Equal(ChangeOutcome.Conflict, await Append().AppendAsync(target.Id, secret.Id, Ct));

        Assert.False(target.IsPrivate);
        Assert.False(_edits.Edits.ContainsKey(target.Id));
        Assert.Equal(0, _documents.Saves);
    }

    private AppendDocument Append() => new(_documents, _edits, _queue, _current, new FixedClock(Now), _log);
}
