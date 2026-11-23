using NoMorePaper.Application.Classification;
using NoMorePaper.Application.Editing;
using NoMorePaper.Application.Trash;
using NoMorePaper.Domain.Documents;
using Xunit;

namespace NoMorePaper.Application.Tests.Trash;

/// <summary>
/// Restoring the original or an edited version (ADR-0025): back to its place, swapped with the version kept now,
/// the archive made anew, and the files moved back when the commit fails.
/// </summary>
public sealed class RestoreTrashFileTests
{
    private readonly TrashScenario _s = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheOriginal_ComesBack_AndTheDocumentKeepsBothVersionsAgain()
    {
        var document = Edited();
        document.ChooseVersions(_s.Current.Id, VersionPolicy.EditedOnly);
        var item = _s.FileTrashed(document, DocumentFileKind.Original);

        Assert.Equal(ChangeOutcome.Changed, await _s.Restore().RestoreAsync(item.Id, Ct));

        Assert.Same(item.File, document.FindFile(DocumentFileKind.Original));
        Assert.Equal(VersionPolicy.KeepBoth, document.VersionPolicy);
        Assert.Equal([$"restore Original {item.Id}"], _s.Storage.Steps);
        Assert.Empty(_s.Trash.Items);
        Assert.Empty(_s.Queue.EditsQueued);
        Assert.Equal(1, _s.Trash.Saves);
        Assert.Equal([5614, 5610], _s.FileLog.EventIds);
    }

    [Fact]
    public async Task AnEditedVersion_TakesThePlaceOfTheCurrentOne_WhichGoesToTheTrash_AndTheArchiveIsMadeAnew()
    {
        var document = Edited("first.pdf");
        var item = _s.FileTrashed(document, DocumentFileKind.Edited);
        var current = TrashScenario.File(DocumentFileKind.Edited, "second.pdf");
        document.ReplaceFile(current, TrashScenario.Now.AddHours(-1));
        _s.Preferences.AfterEditing[_s.Current.Id] = VersionPolicy.OriginalOnly;

        Assert.Equal(ChangeOutcome.Changed, await _s.Restore().RestoreAsync(item.Id, Ct));

        Assert.Equal("first.pdf", document.FindFile(DocumentFileKind.Edited)?.FileName);
        Assert.Equal(TrashScenario.Now, document.EditedAt);
        Assert.Equal(VersionPolicy.KeepBoth, document.VersionPolicy);
        var swapped = Assert.Single(_s.Trash.Items);
        Assert.Equal((TrashItemKind.Edited, current, TrashScenario.Now), (swapped.Kind, swapped.File, swapped.DeletedAt));
        Assert.Equal([$"to-trash Edited {swapped.Id}", $"restore Edited {item.Id}"], _s.Storage.Steps);
        Assert.Equal(EditKind.Rebuild, _s.Edits.Edits[document.Id].Kind);
        Assert.Equal([document.Id], _s.Queue.EditsQueued);
        Assert.Equal([5614, 5610, 5611, 5613], _s.FileLog.EventIds);
    }

    [Fact]
    public async Task AnEditedVersion_WithoutOneKeptNow_ComesBackWithoutSwap()
    {
        var document = Edited();
        var item = _s.FileTrashed(document, DocumentFileKind.Edited);

        Assert.Equal(ChangeOutcome.Changed, await _s.Restore().RestoreAsync(item.Id, Ct));

        Assert.NotNull(document.FindFile(DocumentFileKind.Edited));
        Assert.Null(document.VersionPolicy);
        Assert.Empty(_s.Trash.Items);
        Assert.Equal([$"restore Edited {item.Id}"], _s.Storage.Steps);
    }

    [Fact]
    public async Task WhileAnotherJobOfTheDocumentRuns_AnEditedVersion_StaysInTheTrash()
    {
        var document = Edited();
        var item = _s.FileTrashed(document, DocumentFileKind.Edited);
        _s.Queue.EnqueueEditResult = false;

        Assert.Equal(ChangeOutcome.Conflict, await _s.Restore().RestoreAsync(item.Id, Ct));

        Assert.Null(document.FindFile(DocumentFileKind.Edited));
        Assert.Single(_s.Trash.Items);
        Assert.Empty(_s.Storage.Steps);
        Assert.Equal(0, _s.Trash.Saves);
        Assert.Equal([5615], _s.FileLog.EventIds);
    }

    [Fact]
    public async Task WhileAnEditOfTheDocumentIsPlanned_TheOriginal_StaysInTheTrash()
    {
        var document = Edited();
        var item = _s.FileTrashed(document, DocumentFileKind.Original);
        var planned = _s.PlanEdit(document);

        Assert.Equal(ChangeOutcome.Conflict, await _s.Restore().RestoreAsync(item.Id, Ct));

        Assert.Null(document.FindFile(DocumentFileKind.Original));
        Assert.Null(document.VersionPolicy);
        Assert.Equal([item], _s.Trash.Items);
        Assert.Same(planned, _s.Edits.Edits[document.Id]);
        Assert.Empty(_s.Queue.EditsQueued);
        Assert.Empty(_s.Storage.Steps);
        Assert.Equal(0, _s.Trash.Saves);
        Assert.Equal([5615], _s.FileLog.EventIds);
    }

    [Fact]
    public async Task WhileAnEditOfTheDocumentIsPlanned_AnEditedVersion_StaysInTheTrash_AndThePlannedEditIsKept()
    {
        var document = Edited("first.pdf");
        var item = _s.FileTrashed(document, DocumentFileKind.Edited);
        document.ReplaceFile(TrashScenario.File(DocumentFileKind.Edited, "second.pdf"), TrashScenario.Now.AddHours(-1));
        var planned = _s.PlanEdit(document);

        Assert.Equal(ChangeOutcome.Conflict, await _s.Restore().RestoreAsync(item.Id, Ct));

        Assert.Equal("second.pdf", document.FindFile(DocumentFileKind.Edited)?.FileName);
        Assert.Equal([item], _s.Trash.Items);
        Assert.Same(planned, _s.Edits.Edits[document.Id]);
        Assert.Empty(_s.Queue.EditsQueued);
        Assert.Empty(_s.Storage.Steps);
        Assert.Equal(0, _s.Trash.Saves);
        Assert.Equal([5615], _s.FileLog.EventIds);
    }

    [Fact]
    public async Task AFileOfADocumentInTheTrash_CanOnlyComeBackWithItsDocument()
    {
        var document = Edited();
        var item = _s.FileTrashed(document, DocumentFileKind.Edited);
        _s.Trashed(document);

        Assert.Equal(ChangeOutcome.Conflict, await _s.Restore().RestoreAsync(item.Id, Ct));

        Assert.Equal(2, _s.Trash.Items.Count);
        Assert.Empty(_s.Storage.Steps);
        Assert.Equal(0, _s.Trash.Saves);
    }

    [Fact]
    public async Task AnOriginalWhosePlaceIsTaken_IsAConflict()
    {
        var document = Edited();
        var stray = TrashItemOf(document, TrashScenario.File(DocumentFileKind.Original, "other.pdf"));

        Assert.Equal(ChangeOutcome.Conflict, await _s.Restore().RestoreAsync(stray.Id, Ct));
        Assert.Empty(_s.Storage.Steps);
    }

    [Fact]
    public async Task WhenTheCommitFails_TheFilesMoveBack_AndTheFailureIsReported()
    {
        var document = Edited("first.pdf");
        var item = _s.FileTrashed(document, DocumentFileKind.Edited);
        document.ReplaceFile(TrashScenario.File(DocumentFileKind.Edited, "second.pdf"), TrashScenario.Now.AddHours(-1));
        _s.Trash.SaveFailure = new InvalidOperationException("commit failed");

        await Assert.ThrowsAsync<InvalidOperationException>(() => _s.Restore().RestoreAsync(item.Id, Ct));

        var swapped = _s.Trash.Items.Single(entry => entry.Id != item.Id);
        Assert.Equal(
            [$"to-trash Edited {swapped.Id}", $"restore Edited {item.Id}", $"to-trash Edited {item.Id}", $"restore Edited {swapped.Id}"],
            _s.Storage.Steps);
        Assert.Contains(5616, _s.FileLog.EventIds);
    }

    [Fact]
    public async Task WhenAMoveFails_TheMovesBeforeAreUndone_AndAFailedUndoIsReported()
    {
        var document = Edited("first.pdf");
        var item = _s.FileTrashed(document, DocumentFileKind.Edited);
        document.ReplaceFile(TrashScenario.File(DocumentFileKind.Edited, "second.pdf"), TrashScenario.Now.AddHours(-1));
        _s.Storage.FailingStep = "restore";

        await Assert.ThrowsAsync<IOException>(() => _s.Restore().RestoreAsync(item.Id, Ct));

        // The undo of the first move is a restore too, so it fails as well: reported, not hidden.
        var swapped = _s.Trash.Items.Single(entry => entry.Id != item.Id);
        Assert.Equal([$"to-trash Edited {swapped.Id}", $"restore Edited {item.Id}", $"restore Edited {swapped.Id}"], _s.Storage.Steps);
        Assert.Contains(5617, _s.FileLog.EventIds);
        Assert.Equal(0, _s.Trash.Saves);
    }

    // A done document with its original and an edited version.
    private Document Edited(string fileName = "scan.pdf")
    {
        var document = _s.Done();
        document.ReplaceFile(TrashScenario.File(DocumentFileKind.Edited, fileName), TrashScenario.Now.AddDays(-2));
        return document;
    }

    private TrashItem TrashItemOf(Document document, DocumentFile file)
    {
        var item = TrashItem.ForFile(document, file, TrashScenario.Now.AddDays(-1));
        _s.Trash.Items.Add(item);
        return item;
    }
}
