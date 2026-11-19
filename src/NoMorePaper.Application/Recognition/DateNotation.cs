using System.Globalization;
using System.Text.RegularExpressions;

namespace NoMorePaper.Application.Recognition;

/// <summary>
/// Whether the year of a recognized date is written in a text: as four digits (<c>2026</c>), or as the
/// two-digit year of a short date (<c>19.11.26</c>, <c>19/11/26</c>), common on receipts. A date whose
/// year the text does not name was made up (spike: advertising without year → <c>2023-11-22</c>).
/// </summary>
public static partial class DateNotation
{
    public static bool YearAppearsIn(int year, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var full = year.ToString("0000", CultureInfo.InvariantCulture);
        var shortYear = (year % 100).ToString("00", CultureInfo.InvariantCulture);
        return StandaloneNumbers().Matches(text).Any(match => match.Value == full)
            || ShortDates().Matches(text).Any(match => match.Groups["year"].Value == shortYear);
    }

    [GeneratedRegex(@"(?<!\d)\d{4}(?!\d)")]
    private static partial Regex StandaloneNumbers();

    [GeneratedRegex(@"(?<!\d)\d{1,2}[./-]\d{1,2}[./-](?<year>\d{2})(?!\d)")]
    private static partial Regex ShortDates();
}
