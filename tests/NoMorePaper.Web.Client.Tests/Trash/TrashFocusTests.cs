using NoMorePaper.Contracts.Trash;
using NoMorePaper.Web.Client.Trash;
using Xunit;

namespace NoMorePaper.Web.Client.Tests.Trash;

public sealed class TrashFocusTests
{
    private static TrashItemDto Item(bool documentInTrash = false) =>
        new(Guid.NewGuid(), TrashItemKindDto.Document, "x", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, false, documentInTrash);

    [Fact]
    public void Button_NamesTheTableAndTheCardId()
    {
        var id = Guid.NewGuid();

        Assert.Equal([$"trash-restore-{id:N}", $"trash-card-restore-{id:N}"], TrashFocus.Button(TrashFocus.RestoreAction, id));
    }

    [Theory]
    [InlineData(false, TrashFocus.Heading)]
    [InlineData(true, TrashFocus.Retry)]
    public void AfterReload_IsTheHeading_OrRetryWhenLoadingFailed(bool failed, string expected) =>
        Assert.Equal([expected], TrashFocus.AfterReload(failed));

    [Fact]
    public void AfterCancel_ReturnsToTheButtonThatAsked()
    {
        var id = Guid.NewGuid();

        Assert.Equal([TrashFocus.EmptyButton], TrashFocus.AfterCancel(new TrashQuestion(TrashQuestionKind.Empty, Guid.Empty)));
        Assert.Equal(TrashFocus.Button(TrashFocus.RestoreAction, id), TrashFocus.AfterCancel(new TrashQuestion(TrashQuestionKind.Swap, id)));
        Assert.Equal(TrashFocus.Button(TrashFocus.PurgeAction, id), TrashFocus.AfterCancel(new TrashQuestion(TrashQuestionKind.Purge, id)));
    }

    [Fact]
    public void AfterAction_GoesToTheEntryThatTookThePlace_OrThePreviousOneAtTheEnd()
    {
        TrashItemDto[] items = [Item(), Item()];

        Assert.Equal(TrashFocus.Button(TrashFocus.PurgeAction, items[1].Id), TrashFocus.AfterAction(items, 1, TrashFocus.PurgeAction, false));
        Assert.Equal(TrashFocus.Button(TrashFocus.PurgeAction, items[1].Id), TrashFocus.AfterAction(items, 2, TrashFocus.PurgeAction, false));
        Assert.Equal(TrashFocus.Button(TrashFocus.RestoreAction, items[0].Id), TrashFocus.AfterAction(items, -1, TrashFocus.RestoreAction, false));
    }

    [Fact]
    public void AfterAction_UsesTheDeleteButtonOfAnEntryThatCannotBeRestored()
    {
        TrashItemDto[] items = [Item(documentInTrash: true)];

        Assert.Equal(TrashFocus.Button(TrashFocus.PurgeAction, items[0].Id), TrashFocus.AfterAction(items, 0, TrashFocus.RestoreAction, false));
    }

    [Fact]
    public void AfterAction_WithNothingLeftOrAFailedLoad_GoesToTheHeadingOrRetry()
    {
        Assert.Equal([TrashFocus.Heading], TrashFocus.AfterAction([], 0, TrashFocus.PurgeAction, false));
        Assert.Equal([TrashFocus.Retry], TrashFocus.AfterAction([Item()], 0, TrashFocus.PurgeAction, true));
    }
}
