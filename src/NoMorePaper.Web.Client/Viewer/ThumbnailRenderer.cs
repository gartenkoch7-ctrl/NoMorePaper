using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace NoMorePaper.Web.Client.Viewer;

/// <summary>
/// Renders first-page thumbnails through the viewer's shader (wwwroot/js/viewer/thumbnails.js):
/// only once they become visible, one at a time, cached in the browser's memory.
/// </summary>
public sealed class ThumbnailRenderer(IJSRuntime js, ILogger<ThumbnailRenderer> logger) : IAsyncDisposable
{
    /// <summary>Thumbnail width in CSS pixels; viewer.css (.thumbnail) uses the same value.</summary>
    public const int CssWidth = 80;

    private const string ModulePath = "./js/viewer/thumbnails.js";

    private Task<IJSObjectReference>? _module;

    /// <summary>
    /// Waits until <paramref name="box"/> is visible, then draws the PDF at <paramref name="url"/>
    /// into <paramref name="canvas"/>. True once drawn; false on error or when cancelled first.
    /// </summary>
    public async Task<bool> ShowWhenVisibleAsync(
        ElementReference box, ElementReference canvas, string url, bool originalColors, CancellationToken ct)
    {
        try
        {
            _module ??= js.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask();
            var module = await _module;
            await using var handle = await module.InvokeAsync<IJSObjectReference>(
                "observeThumbnail", ct, box, canvas, url, CssWidth, originalColors);
            await using var cancellation = ct.Register(() => _ = StopAsync(handle));
            var result = await handle.InvokeAsync<string?>("result", CancellationToken.None);
            if (result is null or "ok")
            {
                return result is not null;
            }

            logger.ThumbnailFailed(result);
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (JSException)
        {
            logger.ThumbnailFailed("interop");
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            await (await _module).DisposeAsync();
        }
    }

    private static async Task StopAsync(IJSObjectReference handle)
    {
        try
        {
            await handle.InvokeVoidAsync("dispose");
        }
        catch (JSException)
        {
            // Already gone together with the page.
        }
        catch (ObjectDisposedException)
        {
            // The result arrived at the same moment and the handle was released.
        }
    }
}
