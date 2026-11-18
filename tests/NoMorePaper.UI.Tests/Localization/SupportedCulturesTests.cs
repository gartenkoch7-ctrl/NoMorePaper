using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Localization;

public sealed class SupportedCulturesTests
{
    [Theory]
    [InlineData("de", "de")]
    [InlineData("en", "en")]
    [InlineData("en-US", "en")]
    [InlineData("DE-at", "de")]
    [InlineData("en_GB", "en")]
    [InlineData(" en ", "en")]
    [InlineData("fr", "de")]
    [InlineData("", "de")]
    [InlineData(null, "de")]
    public void Resolve_MapsToSupportedLanguage_OrGermanDefault(string? input, string expected) =>
        Assert.Equal(expected, SupportedCultures.Resolve(input));

    [Theory]
    [InlineData("de", true)]
    [InlineData("en", true)]
    [InlineData("en-US", false)]
    [InlineData("fr", false)]
    [InlineData(null, false)]
    public void IsSupported_AcceptsExactCodesOnly(string? input, bool expected) =>
        Assert.Equal(expected, SupportedCultures.IsSupported(input));

    [Fact]
    public void Default_IsGerman() => Assert.Equal(SupportedCultures.German, SupportedCultures.Default);
}
