using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace NoMorePaper.Web.Client.Viewer;

/// <summary>
/// The per-device switch "original colours" (ADR-0011), kept in the browser's localStorage.
/// Off by default: documents are shown intelligently inverted.
/// </summary>
public sealed class ViewerPreferences(IJSRuntime js, ILogger<ViewerPreferences> logger)
{
    /// <summary>localStorage key; also used by the UI tests to preselect the switch.</summary>
    public const string StorageKey = "nomorepaper.viewer.originalColors";

    private bool? _originalColors;

    public async ValueTask<bool> LoadOriginalColorsAsync()
    {
        if (_originalColors is { } known)
        {
            return known;
        }

        try
        {
            _originalColors = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey) == "true";
        }
        catch (JSException)
        {
            // Storage can be blocked (privacy settings); the viewer must still work.
            logger.ViewerPreferenceUnavailable();
            _originalColors = false;
        }

        return _originalColors.Value;
    }

    public async ValueTask SaveOriginalColorsAsync(bool originalColors)
    {
        _originalColors = originalColors;
        logger.OriginalColorsChanged(originalColors ? "on" : "off");
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", StorageKey, originalColors ? "true" : "false");
        }
        catch (JSException)
        {
            logger.ViewerPreferenceUnavailable();
        }
    }
}
