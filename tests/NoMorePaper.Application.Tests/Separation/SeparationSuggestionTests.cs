using NoMorePaper.Application.Separation;
using NoMorePaper.Domain.Editing;
using Xunit;

namespace NoMorePaper.Application.Tests.Separation;

/// <summary>The suggestion from the page signs and the LLM's sender changes.</summary>
public sealed class SeparationSuggestionTests
{
    [Fact]
    public void ToSuggestion_CutsTheStack_AtEachStart_AndKeepsBlankPagesInsideTheParts()
    {
        var candidates = Candidates(6, new() { [3] = SplitReason.Salutation, [4] = SplitReason.PageCounter }, blank: [5]);

        var suggestion = candidates.ToSuggestion([])!;

        Assert.Equal([new PageRange(1, 2), new PageRange(3, 3), new PageRange(4, 6)], suggestion.Parts);
        Assert.Equal([5], suggestion.BlankPages);
        Assert.Equal([SplitReason.PageCounter, SplitReason.Salutation], suggestion.Reasons);
        Assert.True(suggestion.ToPlan().Validate(6));
    }

    [Fact]
    public void ToSuggestion_AddsTheSenderChanges_OfTheLlm()
    {
        var suggestion = Candidates(4, new() { [3] = SplitReason.Salutation }).ToSuggestion([2, 3])!;

        Assert.Equal([new PageRange(1, 1), new PageRange(2, 2), new PageRange(3, 4)], suggestion.Parts);
        Assert.Equal([SplitReason.Salutation, SplitReason.SenderChange], suggestion.Reasons);
    }

    [Fact]
    public void ToSuggestion_LetsACounterOfAFollowingPage_OverruleEveryOtherSign()
    {
        var candidates = Candidates(3, new() { [3] = SplitReason.Salutation }, continuations: [2, 3]);

        Assert.Null(candidates.ToSuggestion([2]));
    }

    [Fact]
    public void ToSuggestion_MovesAStartOnABlankPage_ToTheNextPageWithContent()
    {
        var suggestion = Candidates(5, new(), blank: [3, 4]).ToSuggestion([3])!;

        Assert.Equal([new PageRange(1, 4), new PageRange(5, 5)], suggestion.Parts);
        Assert.Equal(["1,2", "5"], suggestion.ToPlan().Parts.Select(part => string.Join(',', part.Steps.Select(step => step.SourcePage))));
    }

    [Fact]
    public void ToSuggestion_IgnoresStarts_OnOrBeforeTheFirstPageWithContent_AndOutsideTheStack()
    {
        var candidates = Candidates(4, new() { [1] = SplitReason.PageCounter, [2] = SplitReason.Salutation }, blank: [1]);

        Assert.Null(candidates.ToSuggestion([0, 2, 5, -1]));
    }

    [Fact]
    public void ToSuggestion_IsNull_ForOneDocument_OrABlankStack()
    {
        Assert.Null(Candidates(3, new(), blank: [2]).ToSuggestion([]));
        Assert.Null(Candidates(2, new(), blank: [1, 2]).ToSuggestion([2]));
    }

    private static SeparationCandidates Candidates(
        int pages, Dictionary<int, SplitReason> starts, int[]? continuations = null, int[]? blank = null) =>
        new(pages, starts, (continuations ?? []).ToHashSet(), blank ?? []);
}
