using System.Globalization;
using System.Text.RegularExpressions;

namespace NoMorePaper.Infrastructure.Recognition;

/// <summary>
/// Checks the note of the mail check against the mail text: every date the note names (<c>30.11.</c>,
/// <c>30.11.2026</c>, <c>2026-11-30</c>, <c>30. November</c>) must be a date of the text, as a wrong
/// deadline is worse than none. Day and month are compared, so <c>30.11.26</c> in the text confirms
/// <c>30.11.2026</c> in the note. Pure logic.
/// </summary>
public static partial class MailNoteCheck
{
    private static readonly string[] s_months =
        ["januar", "februar", "märz", "april", "mai", "juni", "juli", "august", "september", "oktober", "november", "dezember"];

    private static readonly string[] s_englishMonths =
        ["january", "february", "march", "april", "may", "june", "july", "august", "september", "october", "november", "december"];

    /// <returns>Whether every date of <paramref name="note"/> is also a date of <paramref name="text"/>.</returns>
    public static bool DatesAreInText(string note, string text)
    {
        ArgumentNullException.ThrowIfNull(note);
        ArgumentNullException.ThrowIfNull(text);
        var known = Dates(text);
        return Dates(note).All(known.Contains);
    }

    /// <summary>
    /// Whether every long number of the note (6 digits or more, groups split by spaces joined) is a
    /// number of the text: the model was seen to add digits to an IBAN, and a wrong account is worse than none.
    /// </summary>
    public static bool NumbersAreInText(string note, string text)
    {
        ArgumentNullException.ThrowIfNull(note);
        ArgumentNullException.ThrowIfNull(text);
        var known = Numbers(text).ToList();
        return Numbers(note).Where(number => number.Length >= 6).All(number => known.Contains(number, StringComparer.Ordinal));
    }

    private static IEnumerable<string> Numbers(string text) =>
        DigitRun().Matches(text).Select(match => match.Value.Replace(" ", string.Empty, StringComparison.Ordinal));

    /// <summary>Day and month of every date in the text.</summary>
    public static HashSet<(int Day, int Month)> Dates(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var dates = new HashSet<(int, int)>();
        foreach (Match match in NumericDate().Matches(text))
        {
            Add(dates, match.Groups["day"].Value, match.Groups["month"].Value);
        }

        foreach (Match match in IsoDate().Matches(text))
        {
            Add(dates, match.Groups["day"].Value, match.Groups["month"].Value);
        }

        foreach (Match match in WrittenDate().Matches(text))
        {
            var word = match.Groups["month"].Value.ToLowerInvariant();
            var month = Array.IndexOf(s_months, word) is var german and >= 0 ? german : Array.IndexOf(s_englishMonths, word);
            if (month >= 0)
            {
                Add(dates, match.Groups["day"].Value, (month + 1).ToString(CultureInfo.InvariantCulture));
            }
        }

        return dates;
    }

    private static void Add(HashSet<(int, int)> dates, string day, string month)
    {
        if (int.TryParse(day, NumberStyles.None, CultureInfo.InvariantCulture, out var d)
            && int.TryParse(month, NumberStyles.None, CultureInfo.InvariantCulture, out var m)
            && d is >= 1 and <= 31 && m is >= 1 and <= 12)
        {
            dates.Add((d, m));
        }
    }

    // 30.11. / 30.11.2026 / 30.11.26 / 1. 12.; not a part of a longer number such as an amount 1.150,00.
    [GeneratedRegex(@"(?<![\d.,])(?<day>\d{1,2})\.\s?(?<month>\d{1,2})\.(?!\d{3}(?!\d))")]
    private static partial Regex NumericDate();

    // Digits with single spaces between groups, as in "DE00 1234 5678" or "3300 5512 77".
    [GeneratedRegex(@"\d+(?: \d+)*")]
    private static partial Regex DigitRun();

    [GeneratedRegex(@"(?<!\d)\d{4}-(?<month>\d{2})-(?<day>\d{2})(?!\d)")]
    private static partial Regex IsoDate();

    [GeneratedRegex(@"(?<!\d)(?<day>\d{1,2})\.?\s+(?<month>[A-Za-zäÄ]{3,9})", RegexOptions.IgnoreCase)]
    private static partial Regex WrittenDate();
}
