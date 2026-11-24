using NoMorePaper.Contracts.Editing;
using NoMorePaper.Web.Client.Editing;
using Xunit;

namespace NoMorePaper.Web.Client.Tests.Editing;

public sealed class PagePlanModelTests
{
    [Fact]
    public void NewModel_HasOnePlainEntryPerPage_AndIsNotDirty()
    {
        var model = new PagePlanModel(3);

        Assert.Equal([1, 2, 3], model.Entries.Select(entry => entry.SourcePage));
        Assert.All(model.Entries, entry => Assert.Equal(new PageEntry(entry.SourcePage), entry));
        Assert.False(model.IsDirty);
        Assert.False(model.CanSave);
        Assert.False(model.CanUndo);
    }

    [Fact]
    public void RotateRight_GoesAroundInFourSteps()
    {
        var model = new PagePlanModel(1);
        var seen = new List<PageRotationDto>();

        for (var i = 0; i < 4; i++)
        {
            model.RotateRight(0);
            seen.Add(model.Entries[0].Rotation);
        }

        Assert.Equal([PageRotationDto.Right, PageRotationDto.Half, PageRotationDto.Left, PageRotationDto.None], seen);
        Assert.False(model.IsDirty);
    }

    [Fact]
    public void RotateLeft_TurnsBackwards()
    {
        var model = new PagePlanModel(1);

        model.RotateLeft(0);

        Assert.Equal(PageRotationDto.Left, model.Entries[0].Rotation);
        Assert.True(model.IsDirty);
    }

    [Fact]
    public void ToggleDelete_MarksAndUnmarks_AndNumbersSkipDeletedPages()
    {
        var model = new PagePlanModel(4);

        model.ToggleDelete(2);

        Assert.True(model.Entries[2].Deleted);
        Assert.Equal(3, model.KeptCount);
        Assert.Equal(4, model.Entries.Count);
        Assert.Equal([1, 2, 2, 3], Enumerable.Range(0, 4).Select(model.NumberOf));
        model.ToggleDelete(2);
        Assert.False(model.Entries[2].Deleted);
        Assert.False(model.IsDirty);
    }

    [Fact]
    public void MoveUp_AndDown_SwapNeighbours_AndRefuseTheEdges()
    {
        var model = new PagePlanModel(3);

        Assert.Equal(-1, model.MoveUp(0));
        Assert.Equal(-1, model.MoveDown(2));
        Assert.Equal(1, model.MoveDown(0));
        Assert.Equal(1, model.MoveUp(2));

        Assert.Equal([2, 3, 1], model.Entries.Select(entry => entry.SourcePage));
        Assert.True(model.IsMoved(0));
        Assert.True(model.IsMoved(2));
        Assert.True(model.IsMoved(1));
        Assert.False(new PagePlanModel(2).IsMoved(1));
    }

    [Fact]
    public void Move_SkipsDeletedPages_AndTheEdgesCountOnlyKeptPages()
    {
        var model = new PagePlanModel(3);
        model.ToggleDelete(1);

        Assert.True(model.CanMoveDown(0));
        Assert.False(model.CanMoveUp(0));
        Assert.True(model.CanMoveUp(2));
        Assert.Equal(0, model.MoveUp(2));
        Assert.Equal([3, 2, 1], model.Entries.Select(entry => entry.SourcePage));
        Assert.True(model.Entries[1].Deleted);
        model.ToggleDelete(0);
        Assert.False(model.CanMoveDown(2));
        Assert.Equal(-1, model.MoveDown(2));
    }

    [Fact]
    public void ReplaceAndClearCrop_SetAndDropTheReplacement()
    {
        var model = new PagePlanModel(2);

        model.Replace(1, "crop-1", "Um 3 Grad gedreht");

        Assert.Equal("crop-1", model.Entries[1].ReplacedJpegKey);
        Assert.Equal("Um 3 Grad gedreht", model.Entries[1].CropText);
        Assert.True(model.ToPlan().Steps[1].Replaced);
        model.ClearCrop(1);
        Assert.Null(model.Entries[1].ReplacedJpegKey);
        Assert.False(model.ToPlan().Steps[1].Replaced);
    }

    [Fact]
    public void Undo_RevertsEveryKindOfChange_OneAfterTheOther()
    {
        var model = new PagePlanModel(3);
        model.RotateRight(0);
        model.ToggleDelete(1);
        model.MoveDown(0);
        model.ToggleSplitAfter(0);
        model.Replace(2, "k", "t");
        model.ClearCrop(2);

        var undone = 0;
        while (model.CanUndo)
        {
            model.Undo();
            undone++;
        }

        Assert.Equal(6, undone);
        Assert.False(model.IsDirty);
        Assert.Null(model.Undo());
        Assert.Equal([1, 2, 3], model.Entries.Select(entry => entry.SourcePage));
        Assert.Empty(model.SplitsAfter);
    }

    [Fact]
    public void Undo_ReturnsTheSourcePageTheUndoneChangeTouched()
    {
        var model = new PagePlanModel(4);
        model.RotateRight(1);
        model.MoveDown(0);
        model.ToggleDelete(3);
        model.ToggleSplitAfter(2);
        model.Replace(2, "k", "t");
        model.RotateLeft(0);

        // Newest first; each answer is the page of that change, not the page of the last action in the UI.
        Assert.Equal(2, model.Undo());
        Assert.Equal(3, model.Undo());
        Assert.Equal(3, model.Undo());
        Assert.Equal(4, model.Undo());
        Assert.Equal(1, model.Undo());
        Assert.Equal(2, model.Undo());
        Assert.Null(model.Undo());
    }

    [Fact]
    public void ToPlan_LeavesOutDeletedPages_AndKeepsTheOrderAndRotation()
    {
        var model = new PagePlanModel(3);
        model.ToggleDelete(0);
        model.RotateLeft(2);
        model.MoveUp(2);

        var plan = model.ToPlan();

        Assert.Equal(
            [new PageStepDto(3, PageRotationDto.Left, false), new PageStepDto(2, PageRotationDto.None, false)],
            plan.Steps);
    }

    [Fact]
    public void AllPagesDeleted_CannotBeSaved_AndToPlanIsEmpty()
    {
        var model = new PagePlanModel(2);
        model.ToggleDelete(0);
        model.ToggleDelete(1);

        Assert.False(model.CanSave);
        Assert.Empty(model.ToPlan().Steps);
        Assert.Equal(0, model.KeptCount);
    }

    [Fact]
    public void Reset_DropsEverything()
    {
        var model = new PagePlanModel(3);
        model.RotateRight(0);
        model.ToggleSplitAfter(0);

        model.Reset();

        Assert.False(model.IsDirty);
        Assert.False(model.CanUndo);
        Assert.Empty(model.SplitsAfter);
    }

    [Fact]
    public void OutOfRangeIndexes_ChangeNothing()
    {
        var model = new PagePlanModel(2);

        model.RotateRight(-1);
        model.ToggleDelete(5);
        model.Replace(2, "k", "t");

        Assert.False(model.IsDirty);
        Assert.False(model.CanUndo);
    }
}
