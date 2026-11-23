using MudBlazor;
using NoMorePaper.Web.Client.Design;
using Xunit;

namespace NoMorePaper.Web.Client.Tests.Documents;

public sealed class StatusIconsTests
{
    [Theory]
    [InlineData(StatusKind.Waiting, nameof(Icons.Material.Filled.Schedule))]
    [InlineData(StatusKind.Processing, nameof(Icons.Material.Filled.Sync))]
    [InlineData(StatusKind.Done, nameof(Icons.Material.Filled.CheckCircle))]
    [InlineData(StatusKind.Failed, nameof(Icons.Material.Filled.Error))]
    [InlineData(StatusKind.Recognized, nameof(Icons.Material.Filled.AutoAwesome))]
    public void For_GivesOneSymbolPerKind(StatusKind kind, string name)
    {
        var expected = (string)typeof(Icons.Material.Filled).GetField(name)!.GetValue(null)!;
        Assert.Equal(expected, StatusIcons.For(kind));
    }

    [Fact]
    public void For_SymbolsAreDistinct() =>
        Assert.Equal(5, Enum.GetValues<StatusKind>().Select(StatusIcons.For).Distinct().Count());
}
