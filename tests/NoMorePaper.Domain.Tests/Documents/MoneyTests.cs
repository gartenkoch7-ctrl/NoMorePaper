using NoMorePaper.Domain.Documents;
using Xunit;

namespace NoMorePaper.Domain.Tests.Documents;

public sealed class MoneyTests
{
    [Theory]
    [InlineData("84.20", "EUR")]
    [InlineData("-12.5", "EUR")]
    [InlineData("0.1234", "USD")]
    [InlineData("1.50000", "CHF")]
    public void Money_KeepsValueAndCurrency(string value, string currency)
    {
        var amount = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

        var money = new Money(amount, currency);

        Assert.Equal((amount, currency), (money.Value, money.Currency));
    }

    [Fact]
    public void Money_ComparesByValue() =>
        Assert.Equal(new Money(84.20m, "EUR"), new Money(84.2m, "EUR"));

    [Theory]
    [InlineData("eur")]
    [InlineData("EURO")]
    [InlineData("€")]
    [InlineData("")]
    public void Money_RejectsAnythingButAnIsoCode(string currency) =>
        Assert.Throws<ArgumentException>(() => new Money(1m, currency));

    [Fact]
    public void Money_RejectsMoreThanFourDecimalPlaces() =>
        Assert.Throws<ArgumentException>(() => new Money(0.12345m, "EUR"));

    [Fact]
    public void Money_RejectsValuesBeyondTheStoredPrecision()
    {
        _ = new Money(Money.MaxAbsoluteValue, "EUR");
        _ = new Money(-Money.MaxAbsoluteValue, "EUR");

        Assert.Throws<ArgumentException>(() => new Money(Money.MaxAbsoluteValue + 0.0001m, "EUR"));
        Assert.Throws<ArgumentException>(() => new Money(-Money.MaxAbsoluteValue - 0.0001m, "EUR"));
    }
}
