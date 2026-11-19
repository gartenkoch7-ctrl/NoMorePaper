using System.Globalization;

namespace NoMorePaper.Server.Documents;

/// <summary>
/// Name of a camera document: "Kamera JJJJ-MM-TT HH-mm.pdf" in the phone's local time, so the title
/// matches the clock the person saw. Pure logic.
/// </summary>
internal static class PageUploadName
{
    /// <summary>A phone clock further off than this is not trusted; the server time is used instead.</summary>
    public static readonly TimeSpan MaxClockDifference = TimeSpan.FromDays(1);

    private static readonly string[] s_formats =
    [
        "yyyy-MM-dd'T'HH:mm:sszzz",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz",
        "yyyy-MM-dd'T'HH:mmzzz",
    ];

    /// <param name="takenAt">
    /// Form field <c>takenAt</c>: ISO 8601 with the phone's UTC offset (<c>2026-11-19T14:30:00+01:00</c>).
    /// Without an offset the local time would be guessed, so such values are not used.
    /// </param>
    /// <param name="serverNow">The server's local time, used when <paramref name="takenAt"/> is missing, malformed or implausible.</param>
    public static string For(string? takenAt, DateTimeOffset serverNow)
    {
        var at = Parse(takenAt) is { } phone && (phone - serverNow).Duration() <= MaxClockDifference
            ? phone
            : serverNow;

        // DateTimeOffset formats in its own offset, i.e. the phone's wall-clock time.
        return string.Create(CultureInfo.InvariantCulture, $"Kamera {at:yyyy-MM-dd HH-mm}.pdf");
    }

    private static DateTimeOffset? Parse(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && DateTimeOffset.TryParseExact(value.Trim(), s_formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
}
