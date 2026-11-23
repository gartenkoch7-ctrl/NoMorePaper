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

        Assert.False(model.MoveUp(0));
        Assert.False(model.MoveDown(2));
        Assert.True(model.MoveDown(0));
        Assert.True(model.MoveUp(2));

        Assert.Equal([2, 3, 1], model.Entries.Select(entry => entry.SourcePage));
        Assert.True(model.IsMoved(0));
        Assert.True(model.IsMoved(2));
        Assert.True(model.IsMoved(1));
        Assert.False(new PagePlanModel(2).IsMoved(1));
    }

    [Fact]
    public void ToggleSplitAfter_SetsAndRemoves_ButNeverAfterTheLastPage()
    {
        var model = new PagePlanModel(3);

        Assert.False(model.ToggleSplitAfter(2));
        Assert.True(model.ToggleSplitAfter(0));
        Assert.Contains(0, model.SplitsAfter);
        Assert.True(model.ToggleSplitAfter(0));

        Assert.Empty(model.SplitsAfter);
        Assert.False(model.IsDirty);
    }

    [Fact]
    public void DocumentNumberOfSplit_CountsTheSplitsUpToIt()
    {
        var model = new PagePlanModel(4);
        model.ToggleSplitAfter(0);
        model.ToggleSplitAfter(2);

        Assert.Equal(1, model.DocumentNumberOfSplit(0));
        Assert.Equal(2, model.DocumentNumberOfSplit(2));
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
        while (model.Undo())
        {
            undone++;
        }

        Assert.Equal(6, undone);
        Assert.False(model.IsDirty);
        Assert.False(model.Undo());
        Assert.Equal([1, 2, 3], model.Entries.Select(entry => entry.SourcePage));
        Assert.Empty(model.SplitsAfter);
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
    public void ToSplit_CutsAtTheSplitPoints_AndDropsPartsWithoutPages()
    {
        var model = new PagePlanModel(5);
        model.ToggleSplitAfter(1);
        model.ToggleSplitAfter(2);
        model.ToggleDelete(2);
        model.ToggleDelete(1);

        Assert.True(model.IsSplit);
        var split = model.ToSplit();

        Assert.Equal(2, split.Parts.Count);
        Assert.Equal([1], split.Parts[0].Steps.Select(step => step.SourcePage));
        Assert.Equal([4, 5], split.Parts[1].Steps.Select(step => step.SourcePage));
    }

    [Fact]
    public void SplitWithOnlyOneKeptPart_IsNotASplit()
    {
        var model = new PagePlanModel(3);
        model.ToggleSplitAfter(0);
        model.ToggleDelete(0);

        Assert.False(model.IsSplit);
        Assert.Equal(1, model.PartCount);
    }

    [Fact]
    public void FromSuggestion_SetsSplitPoints_DeletesBlankPages_AndCanBeUndone()
    {
        var model = new PagePlanModel(5);
        var suggestion = new SplitSuggestionDto([new PageRangeDto(1, 2), new PageRangeDto(3, 5)], [3]);

        model.FromSuggestion(suggestion);

        Assert.Equal([1], model.SplitsAfter);
        Assert.True(model.Entries[2].Deleted);
        Assert.True(model.IsSplit);
        Assert.Equal([[1, 2], [4, 5]], model.ToSplit().Parts.Select(part => part.Steps.Select(step => step.SourcePage).ToArray()));
        Assert.True(model.Undo());
        Assert.False(model.IsDirty);
    }

    [Fact]
    public void FromSuggestion_IgnoresAPartThatEndsOnTheLastPage()
    {
        var model = new PagePlanModel(2);

        model.FromSuggestion(new SplitSuggestionDto([new PageRangeDto(1, 2)], []));

        Assert.Empty(model.SplitsAfter);
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
