using NoMorePaper.Application.Recognition;
using Xunit;

namespace NoMorePaper.Application.Tests.Recognition;

public sealed class AmountNotationTests
{
    [Theory]
    [InlineData("84.20", "Betrag: 84,20 EUR")]
    [InlineData("84.20", "Total 84.20 USD")]
    [InlineData("1150.00", "Gesamtmiete 1.150,00 €")]
    [InlineData("1150.00", "Gesamtmiete 1150,00 €")]
    [InlineData("1150.00", "Total 1,150.00")]
    [InlineData("1150.00", "Miete 1.150,- monatlich")]
    [InlineData("1150.00", "Miete 1150 Euro")]
    [InlineData("1150.00", "Miete: EUR 1.150")]
    [InlineData("50.00", "Spende 50€")]
    [InlineData("1150.00", "Total CHF 1'150.00")]
    [InlineData("1150.00", "Total USD $1,150")]
    [InlineData("1234567.50", "Summe 1.234.567,50")]
    [InlineData("-84.20", "Gutschrift 84,20 EUR")]
    [InlineData("0.5", "Gebühr 0,50 EUR")]
    [InlineData("12.3456", "Kurs 12,3456")]
    [InlineData("84.20", "84,20")]
    public void AppearsIn_FindsTheAmount_InCommonNotations(string amount, string text) =>
        Assert.True(AmountNotation.AppearsIn(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), text));

    [Theory]
    [InlineData("84.20", "Betrag: 184,20 EUR")]
    [InlineData("84.20", "Betrag: 84,205 EUR")]
    [InlineData("84.20", "Betrag: 99,99 EUR")]
    [InlineData("84.00", "Kundennummer 84.200")]
    [InlineData("84.00", "Kundennummer 1984")]
    [InlineData("1150.00", "Rechnung 2026")]
    [InlineData("50.00", "Hauptstraße 50, 80331 München")]
    [InlineData("150.00", "Gesamt 1.150,00 €")]
    [InlineData("150.00", "Total 1,150.00")]
    [InlineData("3150.00", "Menge 3 150,00 EUR")]
    [InlineData("2026.00", "München, 01.11.2026")]
    public void AppearsIn_IgnoresOtherNumbers(string amount, string text) =>
        Assert.False(AmountNotation.AppearsIn(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), text));
}
