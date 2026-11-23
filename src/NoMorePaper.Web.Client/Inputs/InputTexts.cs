using System.Globalization;
using System.Text;
using Microsoft.Extensions.Localization;
using NoMorePaper.Web.Client.Localization;

namespace NoMorePaper.Web.Client.Inputs;

/// <summary>Words for the page "Inputs": what a state, a problem or an age is called. Pure: the clock is the caller's.</summary>
internal static class InputTexts
{
    /// <summary><c>sign-in-failed</c> → <c>InpProblemSignInFailed</c>; the keys of the texts in Strings.resx.</summary>
    public static string ProblemKey(string word)
    {
        var key = new StringBuilder("InpProblem");
        foreach (var part in word.Split('-', StringSplitOptions.RemoveEmptyEntries))
        {
            key.Append(char.ToUpperInvariant(part[0])).Append(part.AsSpan(1));
        }

        return key.ToString();
    }

    /// <summary>The problem in words; a word this client does not know (a newer server) gets a general text.</summary>
    public static string Problem(IStringLocalizer<Strings> localizer, string word)
    {
        var text = localizer[ProblemKey(word)];
        return text.ResourceNotFound ? localizer["InpProblemUnknown"].Value : text.Value;
    }

    public static string Kind(IStringLocalizer<Strings> localizer, string kind) => kind switch
    {
        "scanner-folder" => localizer["InpKindScannerFolder"].Value,
        "mail" => localizer["InpKindMail"].Value,
        "icloud" => localizer["InpKindICloud"].Value,
        _ => kind,
    };

    /// <summary>"vor 5 Minuten": how long ago, rounded down to the unit; "gerade eben" below a minute or for a clock that runs ahead.</summary>
    public static string Age(IStringLocalizer<Strings> localizer, TimeSpan age)
    {
        if (age < TimeSpan.FromMinutes(1))
        {
            return localizer["InpAgeNow"].Value;
        }

        if (age < TimeSpan.FromHours(1))
        {
            var minutes = (int)age.TotalMinutes;
            return minutes == 1 ? localizer["InpAgeMinute"].Value : localizer["InpAgeMinutes", minutes].Value;
        }

        if (age < TimeSpan.FromDays(1))
        {
            var hours = (int)age.TotalHours;
            return hours == 1 ? localizer["InpAgeHour"].Value : localizer["InpAgeHours", hours].Value;
        }

        var days = (int)age.TotalDays;
        return days == 1 ? localizer["InpAgeDay"].Value : localizer["InpAgeDays", days].Value;
    }

    /// <summary>"1 Dokument" / "128 Dokumente": the number formatted for the current culture.</summary>
    public static string Documents(IStringLocalizer<Strings> localizer, long count) =>
        count == 1
            ? localizer["InpDocumentsOne"].Value
            : localizer["InpDocumentsMany", count.ToString("N0", CultureInfo.CurrentCulture)].Value;

    /// <summary>"vor 5 Minuten (19.11.2026 14:03)": the age for a quick sense, the exact time for the record.</summary>
    public static string When(IStringLocalizer<Strings> localizer, DateTimeOffset at, DateTimeOffset now) =>
        localizer["InpWhen", Age(localizer, now - at), at.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)].Value;
}
