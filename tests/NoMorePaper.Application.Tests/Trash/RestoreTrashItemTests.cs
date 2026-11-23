using NoMorePaper.Application.Classification;
using NoMorePaper.Domain.Documents;
using Xunit;

namespace NoMorePaper.Application.Tests.Trash;

/// <summary>Restoring a whole document from the trash (ADR-0025): only the owner, with its lost job back.</summary>
public sealed class RestoreTrashItemTests
{
    private readonly TrashScenario _s = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ADoneDocument_IsVisibleAgain_AndTheEntryIsGone()
    {
        var document = _s.Done();
        document.Classify(new DocumentFields(null, null, null, null, null, "Kurz.", []));
        var item = _s.Trashed(document);

        Assert.Equal(ChangeOutcome.Changed, await _s.Restore().RestoreAsync(item.Id, Ct));

        Assert.False(document.IsInTrash);
        Assert.Empty(_s.Trash.Items);
        Assert.Equal([document.Id], _s.Trash.Locked);
        Assert.Equal(1, _s.Trash.Saves);
        Assert.Empty(_s.Queue.OcrRequeued);
        Assert.Empty(_s.Queue.Requeued);
        Assert.Equal([5610], _s.RestoreLog.EventIds);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ADocumentNotProcessedYet_GetsItsOcrJobAgain(bool started)
    {
        var document = _s.Documents.Add(_s.Current.Id, isPrivate: true);
        if (started)
        {
            document.StartProcessing();
        }

        var item = _s.Trashed(document);

        Assert.Equal(ChangeOutcome.Changed, await _s.Restore().RestoreAsync(item.Id, Ct));

        Assert.Equal([document.Id], _s.Queue.OcrRequeued);
        Assert.Empty(_s.Queue.Requeued);
        Assert.Equal([5610, 5612], _s.RestoreLog.EventIds);
    }

    [Fact]
    public async Task ADoneDocumentWithoutFields_GetsItsRecognitionAgain()
    {
        var document = _s.Done();
        var item = _s.Trashed(document);

        Assert.Equal(ChangeOutcome.Changed, await _s.Restore().RestoreAsync(item.Id, Ct));

        Assert.Equal([document.Id], _s.Queue.Requeued);
        Assert.Empty(_s.Queue.OcrRequeued);
        Assert.Contains(5612, _s.RestoreLog.EventIds);
    }

    [Fact]
    public async Task AnotherAccountsEntry_IsNotFound_AndStaysInTheTrash()
    {
        var bobs = _s.Done(owner: Guid.CreateVersion7());
        var item = _s.Trashed(bobs);

        Assert.Equal(ChangeOutcome.NotFound, await _s.Restore().RestoreAsync(item.Id, Ct));
        Assert.Equal(ChangeOutcome.NotFound, await _s.Restore().RestoreAsync(Guid.CreateVersion7(), Ct));

        Assert.True(bobs.IsInTrash);
        Assert.Single(_s.Trash.Items);
        Assert.Equal(0, _s.Trash.Saves);
        Assert.Equal([5615, 5615], _s.RestoreLog.EventIds);
    }

    [Fact]
    public async Task ARemovedAccount_ChangesNothing()
    {
        var item = _s.Trashed(_s.Done());
        _s.Current.Exists = false;

        Assert.Equal(ChangeOutcome.AccountGone, await _s.Restore().RestoreAsync(item.Id, Ct));

        Assert.Single(_s.Trash.Items);
        Assert.Empty(_s.Trash.Locked);
    }

    [Fact]
    public async Task AnEntryWhoseDocumentIsNotInTheTrash_IsAConflict()
    {
        var document = _s.Done();
        var item = _s.Trashed(document);
        document.RestoreFromTrash(_s.Current.Id);

        Assert.Equal(ChangeOutcome.Conflict, await _s.Restore().RestoreAsync(item.Id, Ct));

        Assert.Single(_s.Trash.Items);
        Assert.Equal(0, _s.Trash.Saves);
    }
}
