using NoMorePaper.Web.Client.Documents;
using Xunit;

namespace NoMorePaper.Web.Client.Tests.Documents;

public sealed class PageWindowTests
{
    [Theory]
    [InlineData(0, 50, 1)]
    [InlineData(50, 50, 1)]
    [InlineData(51, 50, 2)]
    public void TotalPages_RoundsUp_AndIsAtLeastOne(int total, int pageSize, int expected) =>
        Assert.Equal(expected, PageWindow.TotalPages(total, pageSize));

    [Theory]
    [InlineData(1, 50, 120, 1, 50)]
    [InlineData(3, 50, 120, 101, 120)]
    [InlineData(1, 50, 0, 0, 0)]
    public void Range_IsTheRowsOfThePage(int page, int pageSize, int total, int from, int to) =>
        Assert.Equal((from, to), PageWindow.Range(page, pageSize, total));

    [Theory]
    [MemberData(nameof(PageCases))]
    public void Pages_ShowTheWindowAroundTheCurrentPage(int current, int totalPages, int?[] expected) =>
        Assert.Equal(expected, PageWindow.Pages(current, totalPages));

    public static TheoryData<int, int, int?[]> PageCases() => new()
    {
        { 1, 1, [1] },
        { 1, 7, [1, 2, 3, 4, 5, 6, 7] },
        { 1, 21, [1, 2, 3, 4, null, 21] },
        { 3, 21, [1, 2, 3, 4, null, 21] },
        { 4, 21, [1, null, 3, 4, 5, null, 21] },
        { 19, 21, [1, null, 18, 19, 20, 21] },
        { 21, 21, [1, null, 18, 19, 20, 21] },
        { 0, 21, [1, 2, 3, 4, null, 21] },
        { 30, 21, [1, null, 18, 19, 20, 21] },
    };
}
