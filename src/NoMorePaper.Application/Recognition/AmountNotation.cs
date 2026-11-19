using System.Globalization;

namespace NoMorePaper.Application.Recognition;

/// <summary>
/// Whether an amount is written in a text, in German or English notation, with or without thousands
/// separators: 1150.00 is found as <c>1.150,00</c>, <c>1150,00</c>, <c>1,150.00</c> or <c>1150.00</c>,
/// a whole amount also as <c>1.150,-</c>, and without decimals only next to a currency
/// (<c>1.150 €</c>, <c>EUR 1150</c>), because a bare whole number may be a house number or a year.
/// The sign is ignored, because credit notes print the amount without it.
/// </summary>
public static class AmountNotation
{
    private const char NoBreakSpace = (char)0xA0;

    // No plain space: "3 150,00" is mostly a quantity and a price, not 3150.
    private static readonly string[] s_germanGroupSeparators = ["", ".", NoBreakSpace.ToString(), "'"];
    // The apostrophe for Swiss amounts (1'150.00).
    private static readonly string[] s_englishGroupSeparators = ["", ",", "'"];
    private static readonly string[] s_currencies = ["€", "EUR", "Euro", "USD", "$", "CHF", "GBP", "£"];

    public static bool AppearsIn(decimal amount, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        // Amounts below 1000 look the same with every separator; each notation is searched once.
        return Notations(Math.Abs(amount)).Distinct().Any(notation => OccursAlone(text, notation.Text, notation.NeedsCurrency));
    }

    private static IEnumerable<(string Text, bool NeedsCurrency)> Notations(decimal amount)
    {
        var whole = decimal.Truncate(amount);
        var digits = whole.ToString(CultureInfo.InvariantCulture);
        var fraction = (amount - whole).ToString("0.00##", CultureInfo.InvariantCulture)[2..];
        foreach (var separator in s_germanGroupSeparators)
        {
            var integer = Group(digits, separator);
            yield return (integer + "," + fraction, false);
            if (fraction == "00")
            {
                yield return (integer + ",-", false);
                yield return (integer, true);
            }
        }

        foreach (var separator in s_englishGroupSeparators)
        {
            yield return (Group(digits, separator) + "." + fraction, false);
            if (fraction == "00")
            {
                yield return (Group(digits, separator), true);
            }
        }
    }

    private static string Group(string digits, string separator)
    {
        if (separator.Length == 0 || digits.Length <= 3)
        {
            return digits;
        }

        var first = digits.Length % 3 == 0 ? 3 : digits.Length % 3;
        var groups = new List<string> { digits[..first] };
        for (var start = first; start < digits.Length; start += 3)
        {
            groups.Add(digits.Substring(start, 3));
        }

        return string.Join(separator, groups);
    }

    // Not part of a longer number: "84,20" must not match inside "184,20" or "84,205". A separator
    // next to digits continues the number too: "84" in "84.200" is no whole amount, and "150,00" in
    // "1.150,00" is only its last group. A plain space does not count before the amount, since it
    // mostly separates a quantity ("3 150,00").
    private static bool OccursAlone(string text, string notation, bool needsCurrency)
    {
        for (var index = text.IndexOf(notation, StringComparison.Ordinal); index >= 0;
            index = text.IndexOf(notation, index + 1, StringComparison.Ordinal))
        {
            var end = index + notation.Length;
            var before = index > 0 && (char.IsAsciiDigit(text[index - 1])
                || (text[index - 1] is '.' or ',' or '\'' or NoBreakSpace && index > 1 && char.IsAsciiDigit(text[index - 2])));
            var after = end < text.Length && (char.IsAsciiDigit(text[end])
                || (text[end] is '.' or ',' && end + 1 < text.Length && char.IsAsciiDigit(text[end + 1])));
            if (!before && !after && (!needsCurrency || NextToCurrency(text, index, end)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool NextToCurrency(string text, int start, int end)
    {
        var following = text.AsSpan(end).TrimStart();
        var preceding = text.AsSpan(0, start).TrimEnd();
        foreach (var currency in s_currencies)
        {
            if (following.StartsWith(currency, StringComparison.OrdinalIgnoreCase)
                || preceding.EndsWith(currency, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
