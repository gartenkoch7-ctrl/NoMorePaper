using NoMorePaper.Contracts.Editing;
using NoMorePaper.Web.Client.Editing;
using Xunit;

namespace NoMorePaper.Web.Client.Tests.Editing;

public sealed class PagePlanModelSplitTests
{
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
        Assert.Equal([0, 2], model.EffectiveSplitsAfter.Order());
    }

    [Fact]
    public void SplitPointThatOnlyClosesDeletedPages_IsNotEffective_AndDoesNotCount()
    {
        var model = new PagePlanModel(4);
        model.ToggleSplitAfter(0);
        model.ToggleSplitAfter(1);
        model.ToggleDelete(1);

        // Points 0 and 1 are set, but part 2 (page 2) has no page left: one document ends after page 1, none after page 2.
        Assert.Equal([0, 1], model.SplitsAfter.Order());
        Assert.Equal([0], model.EffectiveSplitsAfter.Order());
        Assert.Equal(1, model.DocumentNumberOfSplit(0));
        Assert.Equal(1, model.DocumentNumberOfSplit(2));
        Assert.Equal(2, model.PartCount);
        Assert.True(model.IsSplit);
    }

    [Fact]
    public void SplitPointWithOnlyDeletedPagesBehindIt_IsNotEffective()
    {
        var model = new PagePlanModel(3);
        model.ToggleSplitAfter(0);
        model.ToggleDelete(1);
        model.ToggleDelete(2);

        Assert.Empty(model.EffectiveSplitsAfter);
        Assert.False(model.IsSplit);
        Assert.Equal(1, model.PartCount);
    }

    [Fact]
    public void SplitPointBecomesEffective_WhenTheDeletedPageComesBack()
    {
        var model = new PagePlanModel(2);
        model.ToggleSplitAfter(0);
        model.ToggleDelete(1);
        Assert.Empty(model.EffectiveSplitsAfter);

        model.ToggleDelete(1);

        Assert.Equal([0], model.EffectiveSplitsAfter.Order());
        Assert.True(model.IsSplit);
    }

    [Fact]
    public void EffectiveSplits_AlwaysMatchThePartCount()
    {
        var model = new PagePlanModel(5);
        foreach (var index in new[] { 0, 1, 2, 3 })
        {
            model.ToggleSplitAfter(index);
        }

        model.ToggleDelete(0);
        model.ToggleDelete(2);
        model.ToggleDelete(4);

        Assert.Equal(model.PartCount - 1, model.EffectiveSplitsAfter.Count);
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
        Assert.Null(model.Undo());
        Assert.False(model.IsDirty);
    }

    [Fact]
    public void FromSuggestion_IgnoresAPartThatEndsOnTheLastPage()
    {
        var model = new PagePlanModel(2);

        model.FromSuggestion(new SplitSuggestionDto([new PageRangeDto(1, 2)], []));

        Assert.Empty(model.SplitsAfter);
    }
}
