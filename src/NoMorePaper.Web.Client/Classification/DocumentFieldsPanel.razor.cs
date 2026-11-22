using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using NoMorePaper.Contracts.Classification;
using NoMorePaper.Web.Client.Accessibility;
using NoMorePaper.Web.Client.Localization;

namespace NoMorePaper.Web.Client.Classification;

/// <summary>Code of <see cref="DocumentFieldsPanel"/>: loading, editing, confirming and the owner check.</summary>
public sealed partial class DocumentFieldsPanel
{
    private PendingFocus _focus = default!;
    private DocumentFieldsDto? _fields;
    private LiveRegion? _status;
    private Guid _loadedId;
    private string? _loadError;
    private bool _editing;
    private bool _busy;
    private string? _handedOver;

    [Parameter, EditorRequired] public Guid DocumentId { get; set; }

    [Parameter, EditorRequired] public string Title { get; set; } = string.Empty;

    /// <summary>The signed-in account owns the document: it may choose who else may edit.</summary>
    [Parameter] public bool IsMine { get; set; }

    /// <summary>Saving changed the title (the heading of the page must follow).</summary>
    [Parameter] public EventCallback<string> OnTitleChanged { get; set; }

    /// <summary>The document is private (its owner sees the switch "Privat" in this panel).</summary>
    [Parameter] public bool IsPrivate { get; set; }

    /// <summary>The owner changed the switch "Privat" and the server accepted it.</summary>
    [Parameter] public EventCallback<bool> OnPrivacyChanged { get; set; }

    /// <summary>After every successful load: the page needs kind, source and recognition for path and history.</summary>
    [Parameter] public EventCallback<DocumentFieldsDto> OnFieldsLoaded { get; set; }

    protected override void OnInitialized() => _focus = new PendingFocus(Js);

    protected override Task OnAfterRenderAsync(bool firstRender) => _focus.ApplyAsync();

    protected override async Task OnParametersSetAsync()
    {
        if (DocumentId == _loadedId)
        {
            return;
        }

        // The same component is reused when the route switches to another document.
        _loadedId = DocumentId;
        (_fields, _editing, _handedOver) = (null, false, null);
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loadError = null;
        var result = await Api.GetFieldsAsync(DocumentId);
        if (!result.IsSuccess || result.Value is null)
        {
            _loadError = result.Error;
            return;
        }

        _fields = result.Value;
        var recognition = _fields.Recognition.ToString();
        Logger.FieldsLoaded(recognition);
        await OnFieldsLoaded.InvokeAsync(_fields);
    }

    private Task PrivacyChangedAsync(bool isPrivate) => OnPrivacyChanged.InvokeAsync(isPrivate);

    private void StartEdit() => _editing = true;

    private void CancelEdit()
    {
        _editing = false;
        FocusEdit();
    }

    private async Task SavedAsync(string? newTitle)
    {
        _editing = false;
        await LoadAsync();
        if (newTitle is not null)
        {
            await OnTitleChanged.InvokeAsync(newTitle);
        }

        _ = Counter.RefreshAsync();
        FocusEdit();
        await AnnounceAsync(L["ClsSaved"]);
    }

    private async Task ConfirmAsync()
    {
        if (_fields is null)
        {
            return;
        }

        _busy = true;
        var result = await Api.ConfirmAsync(DocumentId, _fields.Recognition);
        _busy = false;
        if (result.IsConflict)
        {
            Logger.FieldsChangedMeanwhile();
            await ChangedMeanwhileAsync();
            return;
        }

        if (!result.IsSuccess)
        {
            await AnnounceAsync(L["ErrorPrefix", result.Error!]);
            return;
        }

        Logger.FieldsConfirmed();
        await LoadAsync();
        _ = Counter.RefreshAsync();
        FocusEdit();
        await AnnounceAsync(L["ClsConfirmed"]);
    }

    // Recognized, or confirmed for the first time, since the fields were shown: show what is there now and
    // say so, focus back on "Bearbeiten", so nothing is saved or confirmed unseen. If loading them fails,
    // only the error box is shown: focus goes to its "Erneut versuchen" and the text does not claim a reload.
    private async Task ChangedMeanwhileAsync()
    {
        _editing = false;
        await LoadAsync();
        _ = Counter.RefreshAsync();
        if (_loadError is not null)
        {
            _focus.On("fields-retry");
            await AnnounceAsync(L["ClsErrorConflict"]);
            return;
        }

        FocusEdit();
        await AnnounceAsync(L["ClsErrorChangedMeanwhile"]);
    }

    // Kept: reload (the section goes away) and continue at "Bearbeiten". Handed over: the document is
    // gone from our view, so the panel only says where it went and focus lands on that text.
    private async Task OwnerAnsweredAsync(OwnerCheckAnswer answer)
    {
        _ = Counter.RefreshAsync();
        if (answer.HandedOver)
        {
            _handedOver = L["InpOwnerCheckHandedOver", Title, answer.ReceiverName ?? string.Empty];
            // Focus lands on the text, which the screen reader reads: no second announcement.
            _focus.On("fields-handed");
            return;
        }

        await LoadAsync();
        FocusEdit();
        await AnnounceAsync(L["InpOwnerCheckKept", Title]);
    }

    private async Task EditorsSavedAsync(IReadOnlyList<Guid> editorIds)
    {
        if (_fields is not null)
        {
            _fields = _fields with { EditorIds = editorIds };
        }

        await AnnounceAsync(L["ClsEditorsSaved"]);
    }

    // The button that had focus is gone after saving or confirming: continue at "Bearbeiten".
    private void FocusEdit() => _focus.On("fields-edit");

    private async Task AnnounceAsync(string message)
    {
        if (_status is not null)
        {
            await _status.Announce(message);
        }
    }

    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;

    [Inject] private ClassificationApi Api { get; set; } = default!;

    [Inject] private ReviewCounter Counter { get; set; } = default!;

    [Inject] private IJSRuntime Js { get; set; } = default!;

    [Inject] private ILogger<DocumentFieldsPanel> Logger { get; set; } = default!;
}
