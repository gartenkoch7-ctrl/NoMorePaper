using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using NoMorePaper.Contracts.Classification;
using NoMorePaper.Contracts.Documents;
using NoMorePaper.Contracts.Editing;
using NoMorePaper.Web.Client.Accessibility;
using NoMorePaper.Web.Client.Classification;
using NoMorePaper.Web.Client.Documents;
using NoMorePaper.Web.Client.Localization;

namespace NoMorePaper.Web.Client.Editing;

public sealed partial class VersionsSection : IDisposable
{
    private static readonly TimeSpan PollEvery = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PollFor = TimeSpan.FromMinutes(10);

    private LiveRegion? _status;
    private string? _saveError;
    private PendingFocus _focus = default!;
    private CancellationTokenSource? _poll;
    private VersionPolicyDto _policy;
    private VersionPolicyDto _serverPolicy;
    private bool _asking;
    private bool _busy;
    private bool _gaveUp;

    [Parameter, EditorRequired] public DocumentDetailDto Detail { get; set; } = default!;

    /// <summary>The fields of the document; they tell whether the signed-in account may edit it.</summary>
    [Parameter] public DocumentFieldsDto? Fields { get; set; }

    /// <summary>The server reports the edit as done; the page loads the document again.</summary>
    [Parameter] public EventCallback OnEditFinished { get; set; }

    /// <summary>The detail changed without a finished edit (a choice was saved, or polling saw a new state).</summary>
    [Parameter] public EventCallback<DocumentDetailDto> OnDetailChanged { get; set; }

    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;

    [Inject] private EditingApi Editing { get; set; } = default!;

    [Inject] private DocumentApi DocumentsApi { get; set; } = default!;

    [Inject] private IJSRuntime Js { get; set; } = default!;

    [Inject] private ILogger<VersionsSection> Logger { get; set; } = default!;

    private bool HasEdited => Detail.Edited is not null;

    // Only while the document is done and nothing is queued: a running edit accepts no second one.
    private bool CanEditPages => Fields is { CanEdit: true } && Detail.State == DocumentProcessingState.Done && !Detail.EditPending;

    protected override void OnInitialized() => _focus = new PendingFocus(Js);

    protected override void OnParametersSet()
    {
        if (_serverPolicy != Detail.VersionPolicy)
        {
            _serverPolicy = _policy = Detail.VersionPolicy;
            _asking = false;
        }

        if (!Detail.EditPending)
        {
            _gaveUp = false;
        }
        else if (_poll is null && !_gaveUp)
        {
            StartPolling();
        }
    }

    protected override Task OnAfterRenderAsync(bool firstRender) => _focus.ApplyAsync();

    private static string At(DateTimeOffset at) => at.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

    private void OnPolicyChanged(VersionPolicyDto policy)
    {
        _policy = policy;
        _asking = false;
    }

    // Keeping only one version puts the other in the trash: ask first, but not for a harmless choice.
    private Task AskOrSaveAsync()
    {
        if (_policy != VersionPolicyDto.KeepBoth && _policy != _serverPolicy)
        {
            _asking = true;
            return Task.CompletedTask;
        }

        return SaveAsync();
    }

    private void CancelAsking()
    {
        _asking = false;
        _focus.On("versions-save");
    }

    private async Task SaveAsync()
    {
        _asking = false;
        _busy = true;
        _saveError = null;
        _status?.Clear();
        var result = await Editing.ChooseVersionsAsync(Detail.Id, _policy);
        _busy = false;
        _focus.On("versions-save");
        if (!result.IsSuccess)
        {
            Logger.VersionsCallFailed("choose");
            _saveError = L["VerSaveFailed", result.Error ?? L["ErrorUnreachable"]];

            return;
        }

        var chosen = _policy.ToString();
        Logger.VersionPolicySaved(chosen);
        // Fetch the document again: the original may be gone, or a rebuild of the archive may run now.
        var fresh = await DocumentsApi.GetAsync(Detail.Id);
        if (fresh.IsSuccess && fresh.Value is not null)
        {
            await OnDetailChanged.InvokeAsync(fresh.Value);
        }

        if (_status is not null)
        {
            await _status.Announce(L["VerSaved"]);
        }
    }

    private void StartPolling()
    {
        _poll = new CancellationTokenSource();
        _ = PollAsync(_poll.Token);
    }

    private async Task PollAsync(CancellationToken ct)
    {
        var until = DateTimeOffset.UtcNow + PollFor;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(PollEvery, ct);
                if (DateTimeOffset.UtcNow > until)
                {
                    _gaveUp = true;
                    _poll = null;
                    await InvokeAsync(StateHasChanged);
                    return;
                }

                var result = await DocumentsApi.GetAsync(Detail.Id, ct);
                if (!result.IsSuccess || result.Value is null)
                {
                    continue;
                }

                if (result.Value.EditPending)
                {
                    continue;
                }

                _poll = null;
                Logger.EditFinishedDetected();
                await InvokeAsync(() => OnEditFinished.InvokeAsync());
                return;
            }
        }
        catch (OperationCanceledException)
        {
            // The section was removed: nothing left to update.
        }
    }

    public void Dispose()
    {
        _poll?.Cancel();
        _poll?.Dispose();
    }
}
