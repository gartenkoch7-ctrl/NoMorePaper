using NoMorePaper.Application.Classification;
using NoMorePaper.Application.Editing;
using NoMorePaper.Application.Tests.Accounts;
using NoMorePaper.Application.Tests.Classification;
using NoMorePaper.Application.Trash;
using NoMorePaper.Domain.Documents;
using Xunit;

namespace NoMorePaper.Application.Tests.Trash;

/// <summary>Moving a document to the trash (ADR-0025): only the owner, with its job and planned edit dropped.</summary>
public sealed class MoveDocumentToTrashTests
{
    private const string Sha = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
    private static readonly DateTimeOffset s_now = new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeDocumentFieldsStore _documents = new();
    private readonly FakeTrashStore _trash = new();
    private readonly RequeueOnlyJobQueue _queue = new();
    private readonly FakeDocumentEdits _edits = new();
    private readonly FakeCurrentAccount _current = new();
    private readonly RecordingLogger<MoveDocumentToTrash> _log = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheOwner_MovesItToTheTrash_For30Days()
    {
        var document = _documents.Add(_current.Id, isPrivate: true);
        await _edits.SaveAsync(new DocumentEdit(document.Id, EditKind.Pages, "{}", _current.Id, s_now, false), Ct);
        _queue.HadJob = true;

        Assert.Equal(ChangeOutcome.Changed, await Move().MoveAsync(document.Id, Ct));

        Assert.Equal(s_now, document.DeletedAt);
        var item = Assert.Single(_trash.Items);
        Assert.Equal((TrashItemKind.Document, document.Id, "Brief", (DocumentFile?)null), (item.Kind, item.DocumentId, item.Title, item.File));
        Assert.Equal((s_now, s_now.AddDays(30)), (item.DeletedAt, item.PurgeAfter));
        Assert.Equal(7, item.Id.Version);
        Assert.Equal([document.Id], _queue.Cancelled);
        Assert.Equal([document.Id], _edits.Deleted);
        Assert.Equal(1, _documents.Saves);
        Assert.Equal([5600, 5601], _log.EventIds);
    }

    [Fact]
    public async Task WithoutAJob_OnlyTheMoveIsLogged()
    {
        var document = _documents.Add(_current.Id, isPrivate: false);

        Assert.Equal(ChangeOutcome.Changed, await Move().MoveAsync(document.Id, Ct));
        Assert.Equal([5600], _log.EventIds);
    }

    [Fact]
    public async Task AnEditor_IsForbidden_AndAnotherAccountsPrivateDocument_IsNotFound()
    {
        var owner = Guid.CreateVersion7();
        var shared = _documents.Add(owner, isPrivate: false);
        shared.SetEditors(owner, [_current.Id]);
        var secret = _documents.Add(owner, isPrivate: true);

        Assert.Equal(ChangeOutcome.Forbidden, await Move().MoveAsync(shared.Id, Ct));
        Assert.Equal(ChangeOutcome.NotFound, await Move().MoveAsync(secret.Id, Ct));

        Assert.False(shared.IsInTrash || secret.IsInTrash);
        Assert.Empty(_trash.Items);
        Assert.Empty(_queue.Cancelled);
        Assert.Equal(0, _documents.Saves);
    }

    [Fact]
    public async Task ADocumentInTheTrash_IsNotFound_AndARemovedAccount_ChangesNothing()
    {
        var document = _documents.Add(_current.Id, isPrivate: false);
        await Move().MoveAsync(document.Id, Ct);

        Assert.Equal(ChangeOutcome.NotFound, await Move().MoveAsync(document.Id, Ct));

        var other = _documents.Add(_current.Id, isPrivate: false);
        _current.Exists = false;
        Assert.Equal(ChangeOutcome.AccountGone, await Move().MoveAsync(other.Id, Ct));
        Assert.False(other.IsInTrash);
        Assert.Single(_trash.Items);
    }

    [Fact]
    public void TrashItem_ForAFile_KeepsTheFile_AndRefusesTheArchive()
    {
        var document = Document.Receive("Brief", s_now, _current.Id, isPrivate: false);
        var original = new DocumentFile(DocumentFileKind.Original, "scan.pdf", "application/pdf", 10, Sha);
        var edited = new DocumentFile(DocumentFileKind.Edited, "scan.pdf", "application/pdf", 10, Sha);
        var archive = new DocumentFile(DocumentFileKind.Archive, "scan.pdf", "application/pdf", 10, Sha);

        var item = TrashItem.ForFile(document, original, s_now.ToOffset(TimeSpan.FromHours(2)));

        Assert.Equal((TrashItemKind.Original, original, TimeSpan.Zero), (item.Kind, item.File, item.DeletedAt.Offset));
        Assert.Equal(TrashItemKind.Edited, TrashItem.ForFile(document, edited, s_now).Kind);
        Assert.Throws<ArgumentException>(() => TrashItem.ForFile(document, archive, s_now));
        Assert.Equal(s_now.AddDays(30), TrashRules.PurgeAfter(s_now.ToOffset(TimeSpan.FromHours(-5))));
    }

    private MoveDocumentToTrash Move() => new(_documents, _trash, _queue, _edits, _current, new FixedClock(s_now), _log);
}
