namespace NoMorePaper.Web.Client.Localization;

/// <summary>Remembers the user's chosen UI language across visits.</summary>
public interface ICulturePreference
{
    /// <summary>Returns the stored language, or the default if none (or an unsupported one) is stored.</summary>
    ValueTask<string> LoadAsync();

    /// <summary>Stores <paramref name="culture"/>, which must be one of <see cref="SupportedCultures.All"/>.</summary>
    ValueTask SaveAsync(string culture);
}
