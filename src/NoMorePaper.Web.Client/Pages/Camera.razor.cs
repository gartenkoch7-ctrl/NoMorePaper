using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using NoMorePaper.Web.Client.Accessibility;
using NoMorePaper.Web.Client.Camera;
using NoMorePaper.Web.Client.Documents;
using NoMorePaper.Web.Client.Localization;

namespace NoMorePaper.Web.Client.Pages;

/// <summary>
/// <c>/kamera</c>: takes pages with the rear camera, guided by spoken hints and tones, and saves them as
/// one document ("Fertig"). The picture is taken automatically once the sheet is large, fully visible and
/// still; "Auslösen" takes it by hand. Without a camera the page says why and links to the upload.
/// </summary>
public sealed partial class Camera : IAsyncDisposable
{
    // The server takes at most 50 pages per document (PageUploadForm.MaxPages; camera.js checks it too).
    private const int MaxPages = 50;

    // How often the page asks the announcer whether a hint is due; far below its 2.5 s pause.
    private static readonly TimeSpan s_pollInterval = TimeSpan.FromMilliseconds(250);

    private LiveRegion? _status;
    private LiveRegion? _alert;
    private ElementReference _host;
    private ElementReference _captureButton;
    private ElementReference _savedBox;
    private ElementReference _doneButton;
    private PrivateSwitch? _privacy;
    private bool _focusSaved;
    private ITimer? _poll;
    private DateTimeOffset? _takenAt;
    private SavedDocument? _saved;
    private string? _error;
    private string? _hint;
    private bool _tones = true;
    private bool _starting;
    private bool _running;
    private bool _uploading;
    private bool _focusCapture;
    private bool _focusDone;
    private bool _disposed;
    private int _pages;

    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;

    [Inject] private CameraInterop Interop { get; set; } = default!;

    [Inject] private CameraAnnouncer Announcer { get; set; } = default!;

    [Inject] private CameraPreferences Preferences { get; set; } = default!;

    [Inject] private PageUploadApi Upload { get; set; } = default!;

    [Inject] private TimeProvider Time { get; set; } = default!;

    protected override async Task OnInitializedAsync() => _tones = await Preferences.LoadTonesAsync();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await Interop.PrepareAsync();
        }

        // A pressed button that became disabled would drop the focus to the page start.
        if (_focusCapture && _running)
        {
            _focusCapture = false;
            await _captureButton.FocusAsync();
        }
        else if (_focusSaved && _saved is not null)
        {
            _focusSaved = false;
            await _savedBox.FocusAsync();
        }
        else if (_focusDone && _pages > 0 && !_uploading)
        {
            _focusDone = false;
            await _doneButton.FocusAsync();
        }
    }

    private async Task StartAsync()
    {
        (_starting, _error, _saved) = (true, null, null);
        await AnnounceAsync(L["CamStarting"]);
        var error = await Interop.StartAsync(_host, _tones, OnHintAsync, OnCapturedAsync, OnErrorAsync);
        _starting = false;
        if (_disposed || error == CameraInterop.Stopped)
        {
            return;
        }

        if (error is not null)
        {
            await FailAsync(error);
            return;
        }

        _running = true;
        _focusCapture = true;
        Announcer.Announced();
        _poll = Time.CreateTimer(_ => _ = InvokeAsync(AnnounceDueHintAsync), null, s_pollInterval, s_pollInterval);
        await AnnounceAsync(L["CamReady"]);
    }

    // Shown at once as text; spoken when the announcer's pace allows.
    private Task OnHintAsync(string hint)
    {
        Announcer.Show(hint);
        _hint = CameraAnnouncer.KeyFor(hint) is { } key ? L[key].Value : null;
        StateHasChanged();
        return Task.CompletedTask;
    }

    private async Task AnnounceDueHintAsync()
    {
        if (!_uploading && Announcer.Due() is { } key)
        {
            await AnnounceAsync(L[key]);
        }
    }

    // The hint before the picture no longer applies; the camera reports the next one for the next page.
    private async Task OnCapturedAsync(int pages)
    {
        Announcer.Show(null);
        Announcer.Announced();
        _takenAt ??= Time.GetLocalNow();
        (_pages, _saved, _hint) = (pages, null, null);

        // Called from the camera script, not from a page event: the page does not render by itself.
        StateHasChanged();
        await AnnounceAsync(L["CamPageTaken", pages]);
    }

    private async Task OnErrorAsync(string code)
    {
        switch (code)
        {
            case "capture-failed":
                await AlertAsync(L["CamErrorCapture"]);
                return;
            case "too-many-pages":
                Announcer.Announced();
                await AlertAsync(L["CamTooManyPages", MaxPages]);
                return;
        }

        // The camera ended by itself (phone locked, another app): the pages stay, it can be started again.
        await StopCameraAsync();
        await FailAsync(code);
        StateHasChanged();
    }

    private async Task CaptureAsync()
    {
        if (_pages >= MaxPages)
        {
            await AlertAsync(L["CamTooManyPages", MaxPages]);
        }
        else if (!await Interop.CaptureAsync())
        {
            await AlertAsync(L["CamNothingTaken"]);
        }
    }

    private async Task DiscardAsync()
    {
        _pages = await Interop.DiscardLastAsync();
        if (_pages == 0)
        {
            (_takenAt, _focusCapture) = (null, true);
        }

        Announcer.Announced();
        await AnnounceAsync(L["CamDiscarded", _pages]);
    }

    private async Task OnTonesChangedAsync(ChangeEventArgs args)
    {
        _tones = args.Value is true;
        await Preferences.SaveTonesAsync(_tones);
        await Interop.SetTonesAsync(_tones);
    }

    private async Task FailAsync(string code)
    {
        _error = code;
        await AlertAsync(ErrorText(code));
    }

    private string ErrorText(string code) => code switch
    {
        "denied" => L["CamErrorDenied"],
        "no-camera" => L["CamErrorNoCamera"],
        "busy" => L["CamErrorBusy"],
        "unsupported" => L["CamErrorUnsupported"],
        "opencv-failed" => L["CamErrorOpenCv"],
        "ended" => L["CamErrorEnded"],
        _ => L["CamErrorFailed"],
    };

    private async Task AnnounceAsync(string message)
    {
        if (_status is not null && !_disposed)
        {
            await _status.Announce(message);
        }
    }

    private async Task AlertAsync(string message)
    {
        if (_alert is not null && !_disposed)
        {
            await _alert.Announce(message);
        }
    }

    // Stops the camera (its light goes out) and the hint timer; the pages taken so far stay.
    private async Task StopCameraAsync()
    {
        _poll?.Dispose();
        _poll = null;
        (_running, _hint) = (false, null);
        Announcer.Show(null);
        await Interop.StopAsync();
    }

    // Leaving the page drops the pages that were not saved: no picture stays in the browser.
    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        await StopCameraAsync();
        await Interop.ClearAsync(null);
    }
}
