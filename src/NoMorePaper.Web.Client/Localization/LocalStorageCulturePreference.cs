using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using NoMorePaper.Web.Client.Telemetry;

namespace NoMorePaper.Web.Client.Localization;

/// <summary>Keeps the language choice in the browser's localStorage.</summary>
public sealed class LocalStorageCulturePreference(
    IJSRuntime js,
    ILogger<LocalStorageCulturePreference> logger) : ICulturePreference
{
    /// <summary>localStorage key; also used by the UI tests to preselect a language.</summary>
    public const string StorageKey = "nomorepaper.culture";

    public async ValueTask<string> LoadAsync()
    {
        string? stored;
        try
        {
            stored = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        }
        catch (JSException)
        {
            // Storage can be blocked (privacy settings); the app must still start.
            logger.CulturePreferenceUnavailable(SupportedCultures.Default);
            return SupportedCultures.Default;
        }

        if (stored is not null && !SupportedCultures.IsSupported(stored))
        {
            logger.StoredCultureUnsupported(SupportedCultures.Default);
        }

        return SupportedCultures.Resolve(stored);
    }

    public async ValueTask SaveAsync(string culture)
    {
        if (!SupportedCultures.IsSupported(culture))
        {
            throw new ArgumentException($"Unsupported culture '{culture}'.", nameof(culture));
        }

        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", StorageKey, culture);
        }
        catch (JSException)
        {
            logger.CulturePreferenceUnavailable(culture);
        }
    }
}
