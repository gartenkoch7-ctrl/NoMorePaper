using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using NoMorePaper.Contracts.Documents;
using NoMorePaper.Contracts.Editing;
using NoMorePaper.Web.Client.Accessibility;
using NoMorePaper.Web.Client.Classification;
using NoMorePaper.Web.Client.Design;
using NoMorePaper.Web.Client.Documents;
using NoMorePaper.Web.Client.Editing;
using NoMorePaper.Web.Client.Localization;

namespace NoMorePaper.Web.Client.Pages;

/// <summary>
/// <c>/dokumente/{id}/seiten</c>: reorder, rotate and delete the pages of a document, set split points and
/// append another document. Nothing is sent before "Speichern"; every action is announced and keeps focus on
/// something sensible. Unsaved changes are guarded when the page is left (the browser asks on reload or close,
/// the page asks itself on a link). The state lives in <see cref="PagePlanModel"/>.
/// </summary>
public sealed partial class PageEditor
{
    private enum SavedKind
    {
        None,
        Edit,
        Split,
        Append,
    }

    private PendingFocus _focus = default!;
    private LiveRegion? _status;
    private DocumentPagesDto? _pages;
    private DocumentDetailDto? _detail;
    private string? _loadError;
    private string? _saveError;
    private string? _savedText;
    private string? _leaveTarget;
    private Guid _loadedId;
    private SavedKind _saved;
    private bool _loading = true;
    private bool _saving;
    private bool _discarding;
    private bool _appendOpen;
    private bool _leaving;

    [Parameter] public Guid Id { get; set; }

    // The link of a split suggestion is "?trennen=1"; an int, because a bool would throw on "1".
    [Parameter, SupplyParameterFromQuery(Name = "trennen")] public int? Trennen { get; set; }

    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;

    [Inject] private EditingApi Api { get; set; } = default!;

    [Inject] private DocumentApi Documents { get; set; } = default!;

    [Inject] private ClassificationApi Classification { get; set; } = default!;

    [Inject] private NavigationManager Navigation { get; set; } = default!;

    [Inject] private IJSRuntime Js { get; set; } = default!;

    [Inject] private ILogger<PageEditor> Logger { get; set; } = default!;

    private PagePlanModel Model { get; set; } = new(0);

    private bool Guarded => Model.IsDirty && _saved == SavedKind.None;

    private IReadOnlyList<BreadcrumbItem> Crumbs
    {
        get
        {
            var crumbs = new List<BreadcrumbItem> { new(L["NavDocuments"], "dokumente") };
            if (_pages is not null)
            {
                crumbs.Add(new BreadcrumbItem(_pages.Title, DocumentLinks.Viewer(Id)));
            }

            crumbs.Add(new BreadcrumbItem(L["PeTitle"], null));
            return crumbs;
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (Id != _loadedId)
        {
            _loadedId = Id;
            _focus = new PendingFocus(Js);
            await LoadAsync();
        }
    }

    protected override Task OnAfterRenderAsync(bool firstRender) => _focus.ApplyAsync();

    private async Task LoadAsync()
    {
        _loading = true;
        _loadError = null;
        _saved = SavedKind.None;
        var pages = await Api.GetPagesAsync(Id);
        if (!pages.IsSuccess || pages.Value is null)
        {
            _loading = false;
            _loadError = pages.Error ?? string.Empty;
            Logger.PageEditorFailed("load");
            return;
        }

        // Only needed for the privacy hint when appending; without it the hint is left out.
        var detail = await Documents.GetAsync(Id);
        _detail = detail.Value;
        _pages = pages.Value;
        Model = new PagePlanModel(_pages.Pages.Count);
        if (Trennen == 1 && Model.Entries.Count > 1)
        {
            await ApplySuggestionAsync();
        }

        _loading = false;
        Logger.PageEditorLoaded(_pages.Pages.Count);
    }

    // The suggestion belongs to the review list; the pages endpoint does not carry it.
    private async Task ApplySuggestionAsync()
    {
        var review = await Classification.ListReviewAsync();
        if (review.Value?.FirstOrDefault(item => item.Id == Id)?.Split is { } suggestion)
        {
            Model.FromSuggestion(suggestion);
        }
    }

    private PageInfoDto? InfoOf(PageEntry entry) => _pages?.Pages.FirstOrDefault(page => page.Page == entry.SourcePage);

    private async Task SaveAsync()
    {
        if (!Model.CanSave || _saving)
        {
            return;
        }

        _saving = true;
        _saveError = null;
        var split = Model.IsSplit;
        var result = split
            ? await Api.SplitAsync(Id, Model.ToSplit())
            : await Api.SaveEditAsync(Id, Model.ToPlan(), CropImages());
        _saving = false;
        if (!result.IsSuccess)
        {
            Logger.PageEditorFailed(result.IsConflict ? "conflict" : "error");
            _saveError = result.Error;
            _focus.On(PageEditorIds.Save);
            return;
        }

        Logger.PageEditorSaved(Model.KeptCount, Model.PartCount);
        _saved = split ? SavedKind.Split : SavedKind.Edit;
        _savedText = split ? L["PeSavedSplit", Model.PartCount] : L["PeSaved"];
        await Say(_savedText);
        _focus.On("pe-saved-link");
    }

    private void AskDiscard() => _discarding = true;

    private void CancelDiscard()
    {
        _discarding = false;
        _focus.On(PageEditorIds.Discard);
    }

    private async Task DiscardAsync()
    {
        _discarding = false;
        Model.Reset();
        _crops.Clear();
        _focus.On(PageEditorIds.Heading1);
        await Say(L["PeAnnDiscarded"]);
    }

    private void ToggleAppend()
    {
        _appendOpen = !_appendOpen;
        _focus.On(_appendOpen ? PageEditorIds.AppendHeading : PageEditorIds.Append);
    }

    private async Task AppendedAsync(string appendedTitle)
    {
        _appendOpen = false;
        _saved = SavedKind.Append;
        _savedText = L["PeAppended", appendedTitle];
        await Say(_savedText);
        _focus.On("pe-saved-link");
    }

    // Asked for every link inside the app; the page then shows its own question instead of changing the page.
    private Task OnBeforeNavigationAsync(LocationChangingContext context)
    {
        if (Guarded && !_leaving)
        {
            context.PreventNavigation();
            _leaveTarget = context.TargetLocation;
        }

        return Task.CompletedTask;
    }

    private void Leave()
    {
        var target = _leaveTarget;
        _leaveTarget = null;
        _leaving = true;
        if (target is not null)
        {
            Navigation.NavigateTo(target);
        }
    }

    private void CancelLeave()
    {
        _leaveTarget = null;
        _focus.On(PageEditorIds.Heading1);
    }

    private Task Say(string message) => _status?.Announce(message) ?? Task.CompletedTask;
}
