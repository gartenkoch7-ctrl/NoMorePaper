using NoMorePaper.Web.Client.Design;
using Xunit;

namespace NoMorePaper.Web.Client.Tests.Design;

/// <summary>Arrow keys wrap around, Home/End jump, everything else leaves the tab alone.</summary>
public sealed class TabNavigationTests
{
    [Theory]
    [InlineData(0, 3, "ArrowRight", 1)]
    [InlineData(2, 3, "ArrowRight", 0)]
    [InlineData(1, 3, "ArrowLeft", 0)]
    [InlineData(0, 3, "ArrowLeft", 2)]
    [InlineData(1, 3, "Home", 0)]
    [InlineData(1, 3, "End", 2)]
    [InlineData(1, 3, "Enter", 1)]
    [InlineData(1, 3, "a", 1)]
    [InlineData(0, 1, "ArrowRight", 0)]
    [InlineData(0, 0, "ArrowRight", 0)]
    public void Next_MovesAsExpected(int current, int count, string key, int expected)
    {
        Assert.Equal(expected, TabNavigation.Next(current, count, key));
    }
}
