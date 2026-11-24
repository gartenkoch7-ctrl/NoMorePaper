using NoMorePaper.Application.Separation;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Separation;

/// <summary>The page signs alone on the 13 sample stacks: every boundary they can see, and no other.</summary>
public sealed class SeparationSampleTests
{
    [Fact]
    public void TheSamples_AreThirteenScannedStacks()
    {
        var samples = SeparationSamples.Load();

        Assert.Equal(13, samples.Count);
        Assert.All(samples, sample => Assert.True(SeparationHeuristics.IsScannedStack(sample.Pages), sample.Name));
        Assert.All(samples, sample => Assert.Subset(sample.Boundaries.ToHashSet(), sample.SenderOnly.ToHashSet()));
    }

    [Fact]
    public void ThePageSigns_FindEveryBoundaryButTheSenderChanges_AndNoWrongOne()
    {
        foreach (var sample in SeparationSamples.Load())
        {
            var found = SeparationSamples.Boundaries(SeparationHeuristics.Find(sample.Pages).ToSuggestion([]));

            Assert.True(
                sample.Boundaries.Except(sample.SenderOnly).SequenceEqual(found),
                $"{sample.Name}: expected {string.Join(", ", sample.Boundaries.Except(sample.SenderOnly))}, found {string.Join(", ", found)}");
        }
    }

    [Fact]
    public void WithTheSenderChanges_EveryBoundaryIsFound()
    {
        foreach (var sample in SeparationSamples.Load())
        {
            var found = SeparationSamples.Boundaries(SeparationHeuristics.Find(sample.Pages).ToSuggestion(sample.SenderOnly));

            Assert.Equal(sample.Boundaries, found);
        }
    }
}
