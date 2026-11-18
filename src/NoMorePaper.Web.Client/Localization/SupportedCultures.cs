namespace NoMorePaper.Web.Client.Localization;

/// <summary>The UI languages the app ships with. German is the default.</summary>
public static class SupportedCultures
{
    public const string German = "de";
    public const string English = "en";
    public const string Default = German;

    public static IReadOnlyList<string> All { get; } = [German, English];

    /// <summary>
    /// Maps any culture name (e.g. "en-US", "DE") to a supported language code;
    /// unknown or empty names fall back to <see cref="Default"/>.
    /// </summary>
    public static string Resolve(string? cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
        {
            return Default;
        }

        var language = cultureName.Trim().Split('-', '_')[0].ToLowerInvariant();
        return All.Contains(language) ? language : Default;
    }

    /// <summary>True if <paramref name="cultureName"/> names a supported language exactly.</summary>
    public static bool IsSupported(string? cultureName) =>
        cultureName is not null && All.Contains(cultureName);
}
