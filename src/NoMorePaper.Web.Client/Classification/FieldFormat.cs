using System.Globalization;
using NoMorePaper.Contracts.Classification;

namespace NoMorePaper.Web.Client.Classification;

/// <summary>Shows and reads dates and amounts the way the person's language writes them.</summary>
public static class FieldFormat
{
    /// <summary>The API allows at most four decimal places.</summary>
    private const int MaxDecimals = 4;

    public static string Amount(MoneyDto money, CultureInfo culture) =>
        $"{money.Value.ToString("N2", culture)} {money.Currency}";

    public static string Date(DateOnly date, CultureInfo culture) => date.ToString("d", culture);

    /// <summary>The value for an amount input, with the language's decimal separator and no thousands separator.</summary>
    public static string AmountInput(decimal value, CultureInfo culture) => value.ToString("0.00##", culture);

    /// <summary>
    /// Reads an amount typed by hand: the language's separators first ("84,20" in German), then the
    /// invariant ones ("84.20"). More than four decimals are refused, as the server does.
    /// </summary>
    public static bool TryParseAmount(string? text, CultureInfo culture, out decimal value)
    {
        value = 0;
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return false;
        }

        const NumberStyles Styles = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
        var parsed = decimal.TryParse(trimmed, Styles, culture, out value)
            || (!LooksLikeThousands(trimmed, culture) && decimal.TryParse(trimmed, Styles, CultureInfo.InvariantCulture, out value));
        return parsed && decimal.Round(value, MaxDecimals) == value;
    }

    // "1.234" means 1234 to a German reader and 1.234 to an English one. Guessing wrong silently
    // changes the amount, so where the dot is not the decimal separator such a number is refused.
    private static bool LooksLikeThousands(string text, CultureInfo culture)
    {
        var dot = text.IndexOf('.', StringComparison.Ordinal);
        return culture.NumberFormat.NumberDecimalSeparator != "." && dot >= 0 && text.Length - dot - 1 == 3;
    }

    /// <summary>Three letters, as ISO 4217; returned in upper case, or <c>null</c> when it is not.</summary>
    public static string? NormalizeCurrency(string? text)
    {
        var trimmed = text?.Trim().ToUpperInvariant();
        return trimmed is { Length: 3 } && trimmed.All(char.IsAsciiLetter) ? trimmed : null;
    }
}
