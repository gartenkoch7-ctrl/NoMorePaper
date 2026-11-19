using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace NoMorePaper.Web.Client.Camera;

/// <summary>
/// Bridge to the camera module (wwwroot/js/camera/camera.js): rear camera, edge detection with
/// OpenCV.js, guidance, tones and the pictures. One camera at a time; <see cref="StopAsync"/> stops the
/// camera (its light goes out) and frees everything when the page is left.
/// </summary>
public sealed class CameraInterop(IJSRuntime js, ILogger<CameraInterop> logger) : IAsyncDisposable
{
    private const string ModulePath = "./js/camera/camera.js";

    /// <summary>Returned by <see cref="StartAsync"/> when the camera was stopped while it started.</summary>
    public const string Stopped = "stopped";

    private IJSObjectReference? _module;
    private int _generation;
    private IJSObjectReference? _camera;
    private DotNetObjectReference<CameraInterop>? _self;
    private Func<string, Task>? _onHint;
    private Func<int, Task>? _onCaptured;
    private Func<string, Task>? _onError;

    /// <summary>
    /// Loads the module before the start button can be pressed: the press itself then allows sound
    /// (iOS needs a user gesture for it).
    /// </summary>
    public async Task PrepareAsync()
    {
        try
        {
            _module ??= await js.InvokeAsync<IJSObjectReference>("import", ModulePath);
            await _module.InvokeVoidAsync("prepareAudio");
        }
        catch (JSException)
        {
            // StartAsync tries again and reports what fails.
        }
    }

    /// <summary>
    /// Starts the camera in <paramref name="host"/> (an element with a <c>video</c>) and loads the edge detection.
    /// </summary>
    /// <param name="onHint">The hint changed (word of guidance.js, e.g. <c>left</c>).</param>
    /// <param name="onCaptured">A page was taken; the argument is the number of pages now.</param>
    /// <param name="onError">
    /// Something went wrong while it runs: <c>ended</c> (the camera stopped), <c>capture-failed</c>,
    /// <c>too-many-pages</c> (50 taken already).
    /// </param>
    /// <returns><c>null</c> when it runs, otherwise the error code: <c>unsupported</c>, <c>denied</c>,
    /// <c>no-camera</c>, <c>busy</c>, <c>opencv-failed</c>, <c>failed</c>, or <see cref="Stopped"/>.</returns>
    public async Task<string?> StartAsync(
        ElementReference host, bool tones, Func<string, Task> onHint, Func<int, Task> onCaptured, Func<string, Task> onError)
    {
        await StopAsync();
        var generation = _generation;
        _onHint = onHint;
        _onCaptured = onCaptured;
        _onError = onError;
        StartResult result;
        try
        {
            _module ??= await js.InvokeAsync<IJSObjectReference>("import", ModulePath);
            _self ??= DotNetObjectReference.Create(this);
            var camera = await _module.InvokeAsync<IJSObjectReference>("createCamera", host, _self, new { tones });
            if (generation != _generation)
            {
                // Stopped while the module loaded (page left): this camera must never run.
                await camera.DisposeAsync();
                return Stopped;
            }

            _camera = camera;
            result = await camera.InvokeAsync<StartResult>("start");
        }
        catch (JSException)
        {
            result = new StartResult(0, 0, 0, "failed");
        }

        if (generation != _generation)
        {
            // StopAsync ran meanwhile and disposed the camera; start() then stops its stream itself.
            return Stopped;
        }

        if (result.Error is { } error)
        {
            logger.CameraFailed(error);
            await StopAsync();
            return error;
        }

        logger.CameraStarted(result.Width, result.Height, result.OpenCvMs);
        return null;
    }

    /// <summary>The manual shutter. False if no picture was taken.</summary>
    public async Task<bool> CaptureAsync() =>
        _camera is { } camera && await camera.InvokeAsync<bool>("capture");

    /// <returns>The number of pages left; a running camera may take the same sheet again.</returns>
    public async Task<int> DiscardLastAsync() =>
        _module is { } module ? await module.InvokeAsync<int>("discardLast") : 0;

    /// <summary>The JPEG of a page (0-based), fetched only for the upload.</summary>
    public async Task<byte[]> PageAsync(int index) =>
        _module is { } module ? await module.InvokeAsync<byte[]>("page", index) : [];

    /// <summary>
    /// Forgets the first <paramref name="count"/> pages (all with <c>null</c>): after the upload, and when
    /// the page is left (no picture stays in the browser). Pages survive <see cref="StopAsync"/>, so a
    /// camera that stopped by itself can be started again.
    /// </summary>
    /// <returns>The number of pages left.</returns>
    public async Task<int> ClearAsync(int? count) =>
        _module is { } module ? await module.InvokeAsync<int>("clearPages", count) : 0;

    public async Task SetTonesAsync(bool tones)
    {
        if (_camera is { } camera)
        {
            await camera.InvokeVoidAsync("setTones", tones);
        }
    }

    [JSInvokable]
    public Task OnHint(string hint) => _onHint?.Invoke(hint) ?? Task.CompletedTask;

    [JSInvokable]
    public Task OnCaptured(int pages, int elapsedMs)
    {
        logger.PageCaptured(pages, elapsedMs);
        return _onCaptured?.Invoke(pages) ?? Task.CompletedTask;
    }

    [JSInvokable]
    public Task OnError(string code)
    {
        logger.CameraFailed(code);
        return _onError?.Invoke(code) ?? Task.CompletedTask;
    }

    /// <summary>Stops the camera (all tracks, the frame loop, the tones), if one runs; the pages stay.</summary>
    public async Task StopAsync()
    {
        _generation++;
        _onHint = null;
        _onCaptured = null;
        _onError = null;
        var camera = _camera;
        _camera = null;
        if (camera is null)
        {
            return;
        }

        try
        {
            var stats = await camera.InvokeAsync<Stats>("stats");
            if (stats.Frames > 0)
            {
                logger.CameraStopped(stats.Frames, stats.AverageMs);
            }

            await camera.InvokeVoidAsync("dispose");
        }
        catch (JSException)
        {
            // Half-built camera: whatever exists is released with the page.
        }
        finally
        {
            await camera.DisposeAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        try
        {
            await ClearAsync(null);
        }
        catch (JSException)
        {
            // The module is going away with the app; so are its pages.
        }

        if (_module is not null)
        {
            await _module.DisposeAsync();
            _module = null;
        }

        _self?.Dispose();
        _self = null;
    }

    // Shapes of what camera.start() and camera.stats() resolve to.
    private sealed record StartResult(int Width, int Height, int OpenCvMs, string? Error);

    private sealed record Stats(int Frames, int AverageMs);
}
