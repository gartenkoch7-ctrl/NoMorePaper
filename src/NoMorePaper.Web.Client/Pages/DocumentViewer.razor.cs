using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using NoMorePaper.Contracts.Documents;
using NoMorePaper.Web.Client.Accessibility;
using NoMorePaper.Web.Client.Documents;
using NoMorePaper.Web.Client.Localization;
using NoMorePaper.Web.Client.Viewer;

namespace NoMorePaper.Web.Client.Pages;

/// <summary>
/// <c>/dokumente/{id}</c>: shows the document intelligently inverted (ADR-0011), zoomable from
/// 25 to 1600 %, fully usable with keyboard and screen reader. Zoom, page and "no text" are
/// shown as text and announced. Downloads always deliver the unchanged files.
/// </summary>
public sealed partial class DocumentViewer : IAsyncDisposable
{
    private const string PdfMediaType = "application/pdf";

    private DocumentDetailDto? _detail;
    private string? _error;
    private string? _source;
    private bool _opened;
    private bool _showFailed;
    private bool _originalColors;
    private bool _disposed;
    private Guid _loadedId;
    private ViewerState? _state;
    private LiveRegion? _announcer;
    private ElementReference _stage;
    private ElementReference _privateBox;
    private int _privacyVersion;
    private bool _focusPrivateBox;

    [Parameter] public Guid Id { get; set; }

    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;

    [Inject] private DocumentApi Api { get; set; } = default!;

    [Inject] private ViewerInterop Viewer { get; set; } = default!;

    [Inject] private ViewerPreferences Preferences { get; set; } = default!;

    [Inject] private ILogger<DocumentViewer> Logger { get; set; } = default!;

    protected override async Task OnParametersSetAsync()
    {
        if (Id == _loadedId)
        {
            return;
        }

        // The same component is reused when the route switches to another document.
        _loadedId = Id;
        await Viewer.CloseAsync();
        (_detail, _error, _source, _state, _opened, _showFailed) = (null, null, null, null, false, false);
        _originalColors = await Preferences.LoadOriginalColorsAsync();

        var result = await Api.GetAsync(Id);
        if (!result.IsSuccess || result.Value is null)
        {
            _error = result.Error;
            return;
        }

        _detail = result.Value;
        _source = SourceOf(_detail);
        if (_source is null)
        {
            Logger.DocumentNotViewable(_detail.State);
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_focusPrivateBox)
        {
            _focusPrivateBox = false;
            await _privateBox.FocusAsync();
        }

        if (_source is null || _opened || _showFailed)
        {
            return;
        }

        _opened = true;
        var id = Id;
        var shown = await Viewer.OpenAsync(_stage, _source, _originalColors, OnViewChangedAsync, OnRendererChangedAsync);
        if (!shown && !_disposed && id == Id)
        {
            _showFailed = true;
            StateHasChanged();
        }
    }

    private string ZoomText => _state is null ? string.Empty : L["ViewerZoom", _state.ZoomPercent];

    private string PageText => _state is null ? string.Empty : L["ViewerPageOf", _state.Page, _state.PageCount];

    // Shows the archive PDF/A when there is one; a PDF original can be shown before processing.
    private static string? SourceOf(DocumentDetailDto detail) =>
        detail.Archive is not null ? DocumentLinks.Archive(detail.Id)
        : detail.Original.MediaType == PdfMediaType ? DocumentLinks.Original(detail.Id)
        : null;

    private async Task OnViewChangedAsync(ViewerState state)
    {
        var previous = _state;
        _state = state;
        // An unchanged state answers a repeated command (zoom at 1600 %, last page): say both.
        var repeated = previous == state;
        var parts = new List<string>();
        if (previous is null || repeated || previous.Page != state.Page || previous.PageCount != state.PageCount)
        {
            parts.Add(L["ViewerPageOf", state.Page, state.PageCount]);
        }

        if (previous is null || repeated || previous.ZoomPercent != state.ZoomPercent)
        {
            parts.Add(L["ViewerZoom", state.ZoomPercent]);
        }

        if (!state.HasText && (previous is null || previous.Page != state.Page))
        {
            parts.Add(L["ViewerNoText"]);
        }

        StateHasChanged();
        if (_announcer is not null && parts.Count > 0)
        {
            await _announcer.Announce(string.Join(". ", parts));
        }
    }

    // A blind user cannot see the page go black and come back: say what happened.
    private async Task OnRendererChangedAsync(RendererChange change)
    {
        if (_announcer is not null)
        {
            await _announcer.Announce(L["ViewerRenderer" + change]);
        }
    }

    // The checkbox changes the server's state right away; the result is announced, and a failure
    // puts the box back (a new element, because the model value did not change) and keeps focus on it.
    private async Task OnPrivateChangedAsync(ChangeEventArgs args)
    {
        if (_detail is null)
        {
            return;
        }

        var wanted = args.Value is true;
        var result = await Api.SetPrivacyAsync(Id, wanted);
        string message;
        if (result.IsSuccess)
        {
            _detail = _detail with { IsPrivate = wanted };
            message = L[wanted ? "ViewerNowPrivate" : "ViewerNowShared"];
        }
        else
        {
            _privacyVersion++;
            _focusPrivateBox = true;
            message = L["ErrorPrefix", result.Error ?? string.Empty];
        }

        if (_announcer is not null)
        {
            await _announcer.Announce(message);
        }
    }

    private Task ZoomOutAsync() => Viewer.ZoomOutAsync().AsTask();

    private Task ZoomInAsync() => Viewer.ZoomInAsync().AsTask();

    private Task ResetZoomAsync() => Viewer.ResetZoomAsync().AsTask();

    private Task FitWidthAsync() => Viewer.FitWidthAsync().AsTask();

    private async Task GoToPageAsync(int delta)
    {
        if (_state is not null)
        {
            await Viewer.SetPageAsync(_state.Page + delta);
        }
    }

    private async Task ToggleOriginalColorsAsync()
    {
        _originalColors = !_originalColors;
        await Preferences.SaveOriginalColorsAsync(_originalColors);
        await Viewer.SetOriginalColorsAsync(_originalColors);
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        await Viewer.CloseAsync();
    }
}
