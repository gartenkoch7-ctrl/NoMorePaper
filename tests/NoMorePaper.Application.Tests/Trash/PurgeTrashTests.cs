using NoMorePaper.Application.Classification;
using NoMorePaper.Application.Tests.Accounts;
using NoMorePaper.Application.Trash;
using NoMorePaper.Domain.Documents;
using Xunit;

namespace NoMorePaper.Application.Tests.Trash;

/// <summary>
/// Removing for good (ADR-0025): by the owner, by emptying, and by the daily purge after 30 days. Rows go
/// first, then the files; a whole document takes the trash folders of all its entries with it.
/// </summary>
public sealed class PurgeTrashTests
{
    private readonly TrashScenario _s = new();
    private readonly RecordingLogger<PurgeTrashItem> _purgeLog = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AFile_IsRemovedForGood_WithItsTrashFolder()
    {
        var document = _s.Done();
        document.ReplaceFile(TrashScenario.File(DocumentFileKind.Edited), TrashScenario.Now);
        var item = _s.FileTrashed(document, DocumentFileKind.Original);

        Assert.Equal(ChangeOutcome.Changed, await Purge().PurgeAsync(item.Id, Ct));

        Assert.Empty(_s.Trash.Items);
        Assert.Empty(_s.Trash.DeletedDocuments);
        Assert.Equal([$"purge {item.Id}"], _s.Storage.Steps);
        Assert.Equal(1, _s.Trash.Saves);
        Assert.Equal([5618], _s.EraserLog.EventIds);
        Assert.Equal([5621], _purgeLog.EventIds);
    }

    [Fact]
    public async Task ADocument_IsDeletedForGood_WithItsFiles_AndTheFoldersOfAllItsEntries()
    {
        var document = _s.Done();
        document.ReplaceFile(TrashScenario.File(DocumentFileKind.Edited), TrashScenario.Now);
        var original = _s.FileTrashed(document, DocumentFileKind.Original);
        var item = _s.Trashed(document);

        Assert.Equal(ChangeOutcome.Changed, await Purge().PurgeAsync(item.Id, Ct));

        Assert.Equal([document.Id], _s.Trash.DeletedDocuments);
        Assert.Empty(_s.Trash.Items);
        Assert.Equal(1, _s.Trash.Saves);
        Assert.Equal(new HashSet<string> { $"delete {document.Id}", $"purge {original.Id}", $"purge {item.Id}" }, _s.Storage.Steps.ToHashSet());
        Assert.Equal($"delete {document.Id}", _s.Storage.Steps[0]);
        Assert.Equal([5619, 5618], _s.EraserLog.EventIds);
    }

    [Fact]
    public async Task AFile_WhoseDocumentHasAPlannedEdit_IsAConflict_AndStays()
    {
        var document = _s.Done();
        document.ReplaceFile(TrashScenario.File(DocumentFileKind.Edited), TrashScenario.Now);
        var item = _s.FileTrashed(document, DocumentFileKind.Original);
        _s.PlanEdit(document);

        Assert.Equal(ChangeOutcome.Conflict, await Purge().PurgeAsync(item.Id, Ct));

        Assert.Equal([item], _s.Trash.Items);
        Assert.Empty(_s.Storage.Steps);
        Assert.Equal([5625], _s.EraserLog.EventIds);
        Assert.Equal([5615], _purgeLog.EventIds);
    }

    [Fact]
    public async Task FilesThatCannotBeRemoved_AreLogged_TheEntryIsGoneAnyway()
    {
        var item = _s.Trashed(_s.Done());
        _s.Storage.FailingStep = "delete";

        Assert.Equal(ChangeOutcome.Changed, await Purge().PurgeAsync(item.Id, Ct));

        Assert.Empty(_s.Trash.Items);
        Assert.Contains(5620, _s.EraserLog.EventIds);
        Assert.Contains($"purge {item.Id}", _s.Storage.Steps);
    }

    [Fact]
    public async Task AnotherAccountsEntry_IsNotFound_AndARemovedAccount_ChangesNothing()
    {
        var item = _s.Trashed(_s.Done(owner: Guid.CreateVersion7()));

        Assert.Equal(ChangeOutcome.NotFound, await Purge().PurgeAsync(item.Id, Ct));
        Assert.Single(_s.Trash.Items);
        Assert.Empty(_s.Storage.Steps);
        Assert.Equal([5615], _purgeLog.EventIds);

        var mine = _s.Trashed(_s.Done());
        _s.Current.Exists = false;
        Assert.Equal(ChangeOutcome.AccountGone, await Purge().PurgeAsync(mine.Id, Ct));
        Assert.Equal(2, _s.Trash.Items.Count);
    }

    [Fact]
    public async Task EmptyTrash_RemovesEveryEntryOfTheAccount_AndNoOtherAccounts()
    {
        var document = _s.Done();
        document.ReplaceFile(TrashScenario.File(DocumentFileKind.Edited), TrashScenario.Now);
        var original = _s.FileTrashed(document, DocumentFileKind.Original, TrashScenario.Now.AddDays(-2));
        _s.Trashed(document);
        var other = _s.Done();
        other.ReplaceFile(TrashScenario.File(DocumentFileKind.Edited), TrashScenario.Now);
        _s.FileTrashed(other, DocumentFileKind.Original);
        var bobs = _s.Trashed(_s.Done(owner: Guid.CreateVersion7()));
        var log = new RecordingLogger<EmptyTrash>();

        var (outcome, count) = await new EmptyTrash(_s.Trash, _s.Eraser(), _s.Current, log).EmptyAsync(Ct);

        // The original went with its document, so it is not counted on its own; its folder went too.
        Assert.Equal((ChangeOutcome.Changed, 2), (outcome, count));
        Assert.Equal([bobs], _s.Trash.Items);
        Assert.Contains($"purge {original.Id}", _s.Storage.Steps);
        Assert.Equal([5622], log.EventIds);
    }

    [Fact]
    public async Task EmptyTrash_SkipsAFile_WhoseDocumentHasAPlannedEdit()
    {
        var edited = _s.Done();
        edited.ReplaceFile(TrashScenario.File(DocumentFileKind.Edited), TrashScenario.Now);
        var waiting = _s.FileTrashed(edited, DocumentFileKind.Original);
        _s.PlanEdit(edited);
        var gone = _s.Trashed(_s.Done());

        var (outcome, count) = await new EmptyTrash(_s.Trash, _s.Eraser(), _s.Current, new RecordingLogger<EmptyTrash>()).EmptyAsync(Ct);

        Assert.Equal((ChangeOutcome.Changed, 1), (outcome, count));
        Assert.Equal([waiting], _s.Trash.Items);
        Assert.Equal([gone.DocumentId], _s.Trash.DeletedDocuments);
        Assert.DoesNotContain($"purge {waiting.Id}", _s.Storage.Steps);
        Assert.Contains(5625, _s.EraserLog.EventIds);
    }

    [Fact]
    public async Task EmptyTrash_ByARemovedAccount_ChangesNothing()
    {
        _s.Trashed(_s.Done());
        _s.Current.Exists = false;

        var (outcome, _) = await new EmptyTrash(_s.Trash, _s.Eraser(), _s.Current, new RecordingLogger<EmptyTrash>()).EmptyAsync(Ct);

        Assert.Equal(ChangeOutcome.AccountGone, outcome);
        Assert.Single(_s.Trash.Items);
    }

    [Fact]
    public async Task TheDailyPurge_RemovesWhatIsDue_OfEveryAccount_AndKeepsTheRest()
    {
        var due = _s.Trashed(_s.Done(), TrashScenario.Now.AddDays(-31));
        var bobsDue = _s.Trashed(_s.Done(owner: Guid.CreateVersion7()), TrashScenario.Now.AddDays(-30).AddSeconds(-1));
        var kept = _s.Trashed(_s.Done(), TrashScenario.Now.AddDays(-29));
        var log = new RecordingLogger<PurgeDueTrash>();

        var count = await DuePurge(log).PurgeAsync(Ct);

        Assert.Equal(2, count);
        Assert.Equal([kept], _s.Trash.Items);
        Assert.Equal([due.DocumentId, bobsDue.DocumentId], _s.Trash.DeletedDocuments);
        Assert.Equal([5623], log.EventIds);
    }

    [Fact]
    public async Task TheDailyPurge_SkipsADueFile_WhoseDocumentHasAPlannedEdit_UntilALaterRun()
    {
        var edited = _s.Done();
        edited.ReplaceFile(TrashScenario.File(DocumentFileKind.Edited), TrashScenario.Now);
        var waiting = _s.FileTrashed(edited, DocumentFileKind.Original, TrashScenario.Now.AddDays(-31));
        var planned = _s.PlanEdit(edited);
        var due = _s.Trashed(_s.Done(), TrashScenario.Now.AddDays(-31));

        Assert.Equal(1, await DuePurge(new RecordingLogger<PurgeDueTrash>()).PurgeAsync(Ct));
        Assert.Equal([waiting], _s.Trash.Items);
        Assert.Equal([due.DocumentId], _s.Trash.DeletedDocuments);
        Assert.DoesNotContain($"purge {waiting.Id}", _s.Storage.Steps);

        // Once the edit is done, the next run removes it.
        _s.Edits.Edits.Remove(planned.DocumentId);
        Assert.Equal(1, await DuePurge(new RecordingLogger<PurgeDueTrash>()).PurgeAsync(Ct));
        Assert.Empty(_s.Trash.Items);
        Assert.Contains($"purge {waiting.Id}", _s.Storage.Steps);
    }

    [Fact]
    public async Task TheDailyPurge_TakesSeveralRounds_ForALongBacklog()
    {
        var document = _s.Done();
        for (var i = 0; i < PurgeDueTrash.BatchSize + 5; i++)
        {
            _s.Trash.Items.Add(TrashItem.ForFile(document, TrashScenario.File(DocumentFileKind.Edited), TrashScenario.Now.AddDays(-40)));
        }

        Assert.Equal(PurgeDueTrash.BatchSize + 5, await DuePurge(new RecordingLogger<PurgeDueTrash>()).PurgeAsync(Ct));
        Assert.Empty(_s.Trash.Items);
    }

    private PurgeTrashItem Purge() => new(_s.Eraser(), _s.Current, _purgeLog);

    private PurgeDueTrash DuePurge(RecordingLogger<PurgeDueTrash> log) => new(_s.Trash, _s.Eraser(), new FixedClock(TrashScenario.Now), log);
}
