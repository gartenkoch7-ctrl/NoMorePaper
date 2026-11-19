using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace NoMorePaper.Web.Client.Camera;

/// <summary>The per-device switch "tones" of the camera page, kept in the browser's localStorage; on by default.</summary>
public sealed class CameraPreferences(IJSRuntime js, ILogger<CameraPreferences> logger)
{
    /// <summary>localStorage key; also used by the UI tests to preselect the switch.</summary>
    public const string TonesKey = "nomorepaper.camera.tones";

    public async ValueTask<bool> LoadTonesAsync()
    {
        try
        {
            return await js.InvokeAsync<string?>("localStorage.getItem", TonesKey) != "false";
        }
        catch (JSException)
        {
            // Storage can be blocked (privacy settings); the camera must still work.
            logger.CameraPreferenceUnavailable();
            return true;
        }
    }

    public async ValueTask SaveTonesAsync(bool tones)
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", TonesKey, tones ? "true" : "false");
        }
        catch (JSException)
        {
            logger.CameraPreferenceUnavailable();
        }
    }
}
