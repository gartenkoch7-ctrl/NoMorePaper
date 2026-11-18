using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace NoMorePaper.Web.Client.Viewer;

/// <summary>
/// Bridge to the viewer module (wwwroot/js/viewer/viewer.js): PDF.js renders the page, a WebGL
/// shader inverts it intelligently (ADR-0011). One viewer at a time; <see cref="OpenAsync"/>
/// replaces the previous one, <see cref="CloseAsync"/> frees it when the page is left.
/// </summary>
public sealed class ViewerInterop(IJSRuntime js, ILogger<ViewerInterop> logger) : IAsyncDisposable
{
    private const string ModulePath = "./js/viewer/viewer.js";

    private IJSObjectReference? _module;
    private IJSObjectReference? _viewer;
    private DotNetObjectReference<ViewerInterop>? _self;
    private Func<ViewerState, Task>? _onViewChanged;
    private Func<RendererChange, Task>? _onRendererChanged;
    private int _generation;
    private int _lastPage;

    /// <summary>
    /// Shows the PDF at <paramref name="url"/> inside <paramref name="host"/> (an element with an
    /// empty <c>.viewer-canvas</c> and a <c>.viewer-scroller</c>). False if it cannot be shown.
    /// <paramref name="onRendererChanged"/> hears when the WebGL context is lost and rebuilt.
    /// </summary>
    public async Task<bool> OpenAsync(
        ElementReference host,
        string url,
        bool originalColors,
        Func<ViewerState, Task> onViewChanged,
        Func<RendererChange, Task> onRendererChanged)
    {
        try
        {
            return await OpenCoreAsync(host, url, originalColors, onViewChanged, onRendererChanged);
        }
        catch (JSException)
        {
            // Module not loadable or the browser lacks something PDF.js needs.
            logger.ViewerOpenFailed("interop");
            await CloseAsync();
            return false;
        }
    }

    private async Task<bool> OpenCoreAsync(
        ElementReference host,
        string url,
        bool originalColors,
        Func<ViewerState, Task> onViewChanged,
        Func<RendererChange, Task> onRendererChanged)
    {
        await CloseAsync();
        var generation = _generation;
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", ModulePath);
        _self ??= DotNetObjectReference.Create(this);
        var viewer = await _module.InvokeAsync<IJSObjectReference>("createViewer", host, _self, originalColors);
        if (generation != _generation)
        {
            // Closed while the module was loading: the page is gone.
            await DisposeViewerAsync(viewer);
            return false;
        }

        _viewer = viewer;
        _onViewChanged = onViewChanged;
        _onRendererChanged = onRendererChanged;
        var result = await viewer.InvokeAsync<OpenResult>("open", url);
        if (generation != _generation)
        {
            return false;
        }

        if (result.Error is { } error)
        {
            logger.ViewerOpenFailed(error);
            await CloseAsync();
            return false;
        }

        logger.ViewerOpened(result.PageCount, result.Renderer ?? "unknown");
        if (result.FallbackReason is { } reason)
        {
            logger.WebGlFallback(reason);
        }

        return true;
    }

    public ValueTask ZoomInAsync() => CallAsync("zoomIn");

    public ValueTask ZoomOutAsync() => CallAsync("zoomOut");

    public ValueTask ResetZoomAsync() => CallAsync("reset");

    public ValueTask FitWidthAsync() => CallAsync("fitWidth");

    public ValueTask SetPageAsync(int page) => CallAsync("setPage", page);

    public ValueTask SetOriginalColorsAsync(bool originalColors) => CallAsync("setOriginalColors", originalColors);

    [JSInvokable]
    public Task OnViewChanged(int zoomPercent, int page, int pageCount, bool hasText)
    {
        // Once per page shown, not again for every zoom step on it.
        if (!hasText && page != _lastPage)
        {
            logger.PageWithoutText();
        }

        _lastPage = page;
        return _onViewChanged?.Invoke(new ViewerState(zoomPercent, page, pageCount, hasText)) ?? Task.CompletedTask;
    }

    [JSInvokable]
    public void OnRendered(int durationMs) => logger.PageRendered(durationMs);

    [JSInvokable]
    public void OnError(string errorType) => logger.ViewerRenderFailed(errorType);

    /// <summary>change: <c>lost</c>, <c>restored</c> or <c>fallback</c> (then with the reason).</summary>
    [JSInvokable]
    public Task OnRendererChanged(string change, string? reason)
    {
        switch (change)
        {
            case "lost":
                logger.WebGlContextLost();
                return NotifyAsync(RendererChange.Lost);
            case "restored":
                logger.WebGlContextRestored();
                return NotifyAsync(RendererChange.Restored);
            case "fallback":
                logger.WebGlFallback(reason ?? "unknown");
                return NotifyAsync(RendererChange.Fallback);
            default:
                return Task.CompletedTask;
        }
    }

    private Task NotifyAsync(RendererChange change) => _onRendererChanged?.Invoke(change) ?? Task.CompletedTask;

    /// <summary>Frees the current viewer (PDF.js document, textures, listeners), if any.</summary>
    public async Task CloseAsync()
    {
        _generation++;
        _lastPage = 0;
        _onViewChanged = null;
        _onRendererChanged = null;
        var viewer = _viewer;
        _viewer = null;
        if (viewer is not null)
        {
            await DisposeViewerAsync(viewer);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync();
        if (_module is not null)
        {
            await _module.DisposeAsync();
        }

        _self?.Dispose();
    }

    private async ValueTask CallAsync(string method, params object?[] args)
    {
        if (_viewer is { } viewer)
        {
            await viewer.InvokeVoidAsync(method, args);
        }
    }

    private static async Task DisposeViewerAsync(IJSObjectReference viewer)
    {
        try
        {
            await viewer.InvokeVoidAsync("dispose");
        }
        catch (JSException)
        {
            // Half-built viewer: whatever exists is released with the page.
        }
        finally
        {
            await viewer.DisposeAsync();
        }
    }

    // Shape of the object viewer.open() resolves to.
    private sealed record OpenResult(int PageCount, string? Renderer, string? FallbackReason, string? Error);
}
