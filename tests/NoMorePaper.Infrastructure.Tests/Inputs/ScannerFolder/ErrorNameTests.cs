using System.Text;
using NoMorePaper.Infrastructure.Inputs.ScannerFolder;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Inputs.ScannerFolder;

/// <summary>Names of files moved to the error folder.</summary>
public sealed class ErrorNameTests
{
    private static readonly DateTimeOffset s_at = new(2026, 11, 19, 15, 30, 5, TimeSpan.FromHours(1));

    [Theory]
    [InlineData(0, "20261119-143005-notiz.txt")]
    [InlineData(2, "20261119-143005-2-notiz.txt")]
    public void TheName_StartsWithTheUtcTime_AndACounterOnClashes(int attempt, string expected) =>
        Assert.Equal(expected, ScannerFileIntake.ErrorName("notiz.txt", s_at, attempt));

    [Fact]
    public void ALongName_IsShortened_KeepingItsExtension_AndWholeCharacters()
    {
        // The cut lands right behind a two-unit emoji: it must go as a whole.
        var name = string.Concat(Enumerable.Repeat("ä😀", 100)) + ".docx";

        var shortened = ScannerFileIntake.ErrorName(name, s_at, 0);

        Assert.True(Encoding.UTF8.GetByteCount(shortened) <= 255);
        Assert.EndsWith("ä.docx", shortened, StringComparison.Ordinal);
        Assert.Equal(shortened, Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(shortened)));
    }

    [Fact]
    public void AnOverlongExtension_IsNotKeptApart()
    {
        var name = "brief." + new string('x', 300);

        var shortened = ScannerFileIntake.ErrorName(name, s_at, 0);

        Assert.True(Encoding.UTF8.GetByteCount(shortened) <= 255);
        Assert.StartsWith("20261119-143005-brief.", shortened, StringComparison.Ordinal);
    }
}
