namespace NoMorePaper.Domain.Documents;

/// <summary>An amount of money in one currency, e.g. 84.20 EUR.</summary>
public sealed record Money
{
    /// <summary>Most decimal places kept (stored as <c>numeric(18, 4)</c>).</summary>
    public const int MaxDecimalPlaces = 4;

    /// <summary>Largest absolute value that fits the stored precision.</summary>
    public const decimal MaxAbsoluteValue = 99_999_999_999_999.9999m;

    /// <param name="value">The amount; negative for credit notes. At most <see cref="MaxDecimalPlaces"/> decimal places.</param>
    /// <param name="currency">ISO 4217 code: three upper-case letters, e.g. <c>EUR</c>.</param>
    /// <exception cref="ArgumentException">The value or the currency is invalid.</exception>
    public Money(decimal value, string currency)
    {
        if (Math.Abs(value) > MaxAbsoluteValue || decimal.Round(value, MaxDecimalPlaces) != value)
        {
            throw new ArgumentException($"At most {MaxDecimalPlaces} decimal places and {MaxAbsoluteValue} in value.", nameof(value));
        }

        if (currency is not { Length: 3 } || !currency.All(c => c is >= 'A' and <= 'Z'))
        {
            throw new ArgumentException("Expected an ISO 4217 code of three upper-case letters.", nameof(currency));
        }

        Value = value;
        Currency = currency;
    }

    public decimal Value { get; }

    public string Currency { get; }
}
