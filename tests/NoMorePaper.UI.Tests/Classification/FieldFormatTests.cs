using System.Globalization;
using NoMorePaper.Contracts.Classification;
using NoMorePaper.Web.Client.Classification;
using Xunit;

namespace NoMorePaper.UI.Tests.Classification;

/// <summary>Reading and showing amounts and dates, and building the filter query; no browser needed.</summary>
public sealed class FieldFormatTests
{
    private static readonly CultureInfo _german = CultureInfo.GetCultureInfo("de");
    private static readonly CultureInfo _english = CultureInfo.GetCultureInfo("en");

    [Theory]
    [InlineData("84,20", 84.20)]
    [InlineData("84.20", 84.20)]
    [InlineData(" 1000 ", 1000)]
    [InlineData("-12,5", -12.5)]
    [InlineData("0,0001", 0.0001)]
    public void TryParseAmount_ReadsGermanAndInvariantWriting(string text, double expected)
    {
        Assert.True(FieldFormat.TryParseAmount(text, _german, out var value));
        Assert.Equal((decimal)expected, value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("1,23456")]
    [InlineData("12 EUR")]
    [InlineData("1.234")]
    public void TryParseAmount_RefusesWhatIsNoAmount_OrHasTooManyDecimals(string? text)
        => Assert.False(FieldFormat.TryParseAmount(text, _german, out _));

    [Fact]
    public void Amount_ShowsTwoDecimalsInTheLanguagesWriting_WithTheCurrency()
    {
        Assert.Equal("84,20 EUR", FieldFormat.Amount(new MoneyDto(84.2m, "EUR"), _german));
        Assert.Equal("1,234.50 USD", FieldFormat.Amount(new MoneyDto(1234.5m, "USD"), _english));
    }

    [Fact]
    public void AmountInput_KeepsDecimalsButNoThousandsSeparator()
    {
        Assert.Equal("1234,50", FieldFormat.AmountInput(1234.5m, _german));
        Assert.Equal("0.0001", FieldFormat.AmountInput(0.0001m, _english));
    }

    [Theory]
    [InlineData("eur", "EUR")]
    [InlineData(" chf ", "CHF")]
    [InlineData("EURO", null)]
    [InlineData("E1R", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void NormalizeCurrency_NeedsThreeLetters(string? text, string? expected)
        => Assert.Equal(expected, FieldFormat.NormalizeCurrency(text));

    [Fact]
    public void FilterQuery_WritesTheContractNames_WithInvariantValues()
    {
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());
        var filter = new DocumentFilterDto(a, b, new DateOnly(2026, 1, 2), new DateOnly(2026, 12, 31), [a, b], true, 50.5m, 1000m, false);

        var query = DocumentFilterQuery.Build(filter);

        Assert.Equal(
            $"&correspondentId={a}&documentTypeId={b}&from=2026-01-02&to=2026-12-31&tagIds={a},{b}&allTags=true"
            + "&amountFrom=50.5&amountTo=1000&isPrivate=false",
            query);
        Assert.Equal(8, DocumentFilterQuery.ActiveCount(filter));
    }

    [Fact]
    public void FilterQuery_WithoutFilters_IsEmpty_AndAllTagsAloneIsNoFilter()
    {
        Assert.Equal(string.Empty, DocumentFilterQuery.Build(null));
        Assert.Equal(string.Empty, DocumentFilterQuery.Build(new DocumentFilterDto(AllTags: true)));
        Assert.Equal(0, DocumentFilterQuery.ActiveCount(new DocumentFilterDto(AllTags: true)));
        Assert.Equal(0, DocumentFilterQuery.ActiveCount(null));
    }
}
