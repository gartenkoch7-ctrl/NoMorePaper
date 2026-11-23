using NoMorePaper.Domain.Editing;
using Xunit;

namespace NoMorePaper.Domain.Tests.Editing;

/// <summary>Page plans, split plans and page ranges: which plans fit a source of n pages.</summary>
public sealed class PagePlanTests
{
    [Fact]
    public void PagePlan_FitsReorderedTurnedAndDeletedPages()
    {
        var plan = Plan(Step(3, PageRotation.Right), Step(1), Step(2, PageRotation.Left, replaced: true));

        Assert.True(plan.Validate(3));
        Assert.True(Plan(Step(2)).Validate(3));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void PagePlan_RefusesPagesOutsideTheSource(int page) =>
        Assert.False(Plan(Step(1), Step(page)).Validate(3));

    [Fact]
    public void PagePlan_RefusesNoPages_TwiceTheSamePage_AndUnknownTurns()
    {
        Assert.False(new PagePlan([]).Validate(3));
        Assert.False(Plan(Step(1), Step(1)).Validate(3));
        Assert.False(Plan(Step(1, (PageRotation)45)).Validate(3));
    }

    [Fact]
    public void SplitPlan_NeedsTwoParts_EachPageAtMostOnce()
    {
        Assert.True(new SplitPlan([Plan(Step(1), Step(2)), Plan(Step(4))]).Validate(4));
        Assert.False(new SplitPlan([Plan(Step(1), Step(2))]).Validate(4));
        Assert.False(new SplitPlan([Plan(Step(1)), Plan(Step(1))]).Validate(4));
        Assert.False(new SplitPlan([Plan(Step(1)), new PagePlan([])]).Validate(4));
        Assert.False(new SplitPlan([Plan(Step(1)), Plan(Step(5))]).Validate(4));
    }

    [Fact]
    public void PageRange_ListsItsPages_AndRefusesReversedRanges()
    {
        Assert.Equal([4, 5, 6], new PageRange(4, 6).Pages());
        Assert.Equal([2], new PageRange(2, 2).Pages());
        Assert.Throws<ArgumentOutOfRangeException>(() => new PageRange(0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PageRange(3, 2));
    }

    [Fact]
    public void SplitSuggestion_ToPlan_LeavesBlankPagesOut()
    {
        var suggestion = new SplitSuggestion(
            [new PageRange(1, 2), new PageRange(3, 3), new PageRange(4, 6)], [3, 5], [SplitReason.Blank, SplitReason.PageCounter]);

        var plan = suggestion.ToPlan();

        Assert.Equal(2, plan.Parts.Count);
        Assert.Equal([1, 2], plan.Parts[0].Steps.Select(step => step.SourcePage));
        Assert.Equal([4, 6], plan.Parts[1].Steps.Select(step => step.SourcePage));
        Assert.All(plan.Parts.SelectMany(part => part.Steps), step => Assert.Equal((PageRotation.None, false), (step.Rotation, step.Replaced)));
        Assert.True(plan.Validate(6));
    }

    private static PagePlan Plan(params PageStep[] steps) => new(steps);

    private static PageStep Step(int page, PageRotation rotation = PageRotation.None, bool replaced = false) => new(page, rotation, replaced);
}
