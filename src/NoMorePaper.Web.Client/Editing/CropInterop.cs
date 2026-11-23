using Microsoft.JSInterop;

namespace NoMorePaper.Web.Client.Editing;

/// <summary>
/// Bridge to the crop module (wwwroot/js/editing/crop.js): draws a page of the PDF at 200 dpi, looks for the
/// sheet with the camera's edge detection (OpenCV.js), straightens the chosen frame to a JPEG and reports corners
/// dragged with the mouse. One page at a time; the drawn page stays in the browser until it is applied or released.
/// </summary>
public sealed class CropInterop(IJSRuntime js) : IAsyncDisposable
{
    private const string ModulePath = "./js/editing/crop.js";

    private IJSObjectReference? _module;
    private DotNetObjectReference<CropInterop>? _self;
    private Func<CropCorner, CropPoint, bool, Task>? _onDrag;

    /// <summary>
    /// Draws page <paramref name="page"/> (from 1) of the PDF at <paramref name="url"/> and looks for the sheet on it.
    /// </summary>
    /// <param name="onDrag">
    /// A corner handle was dragged: the corner as seen, where it is now on the turned page, and whether the
    /// mouse was let go.
    /// </param>
    public async Task<CropDetection> DetectAsync(string url, int page, Func<CropCorner, CropPoint, bool, Task> onDrag)
    {
        _onDrag = onDrag;
        try
        {
            _module ??= await js.InvokeAsync<IJSObjectReference>("import", ModulePath);
            _self ??= DotNetObjectReference.Create(this);
            var found = await _module.InvokeAsync<Detected>("detect", url, page, _self);
            if (found.Error is { } error || found.Corners is not { Length: 8 } corners)
            {
                return new CropDetection(false, null, found.ElapsedMs, found.Error ?? "interop");
            }

            var shape = new CropShape(CropQuad.FromFractions(corners), found.WidthPt, found.HeightPt);
            return new CropDetection(found.Found, shape, found.ElapsedMs, null);
        }
        catch (Exception exception) when (exception is JSException or ArgumentException)
        {
            return new CropDetection(false, null, 0, "interop");
        }
    }

    /// <summary>Straightens <paramref name="quad"/> of the drawn page; the JPEG (with JFIF header), or <c>null</c> when it failed.</summary>
    public async Task<byte[]?> ApplyAsync(CropQuad quad)
    {
        ArgumentNullException.ThrowIfNull(quad);
        if (_module is not { } module)
        {
            return null;
        }

        try
        {
            var jpeg = await module.InvokeAsync<byte[]?>("apply", quad.ToFractions());
            return jpeg is { Length: > 0 } ? jpeg : null;
        }
        catch (JSException)
        {
            return null;
        }
    }

    /// <summary>Forgets the drawn page (cancelled), so its pixels do not stay in memory.</summary>
    public async Task ReleaseAsync()
    {
        _onDrag = null;
        if (_module is { } module)
        {
            try
            {
                await module.InvokeVoidAsync("release");
            }
            catch (JSException)
            {
                // Gone with the page.
            }
        }
    }

    [JSInvokable]
    public Task OnDrag(int corner, double x, double y, bool done)
    {
        if (_onDrag is not { } onDrag || corner is < 0 or > 3 || !double.IsFinite(x) || !double.IsFinite(y))
        {
            return Task.CompletedTask;
        }

        var point = new CropPoint(
            (int)Math.Round(Math.Clamp(x, 0, 1) * CropPoint.Scale), (int)Math.Round(Math.Clamp(y, 0, 1) * CropPoint.Scale));
        return onDrag((CropCorner)corner, point, done);
    }

    public async ValueTask DisposeAsync()
    {
        await ReleaseAsync();
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The app is closing.
            }

            _module = null;
        }

        _self?.Dispose();
        _self = null;
    }

    // Shape of what crop.js detect() resolves to.
    private sealed record Detected(bool Found, double[]? Corners, double WidthPt, double HeightPt, int ElapsedMs, string? Error);
}
