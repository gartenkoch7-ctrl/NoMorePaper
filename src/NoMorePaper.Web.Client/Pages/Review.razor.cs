using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using NoMorePaper.Contracts.Accounts;
using NoMorePaper.Contracts.Classification;
using NoMorePaper.Web.Client.Accessibility;
using NoMorePaper.Web.Client.Classification;
using NoMorePaper.Web.Client.Documents;
using NoMorePaper.Web.Client.Localization;

namespace NoMorePaper.Web.Client.Pages;

/// <summary>
/// <c>/zu-pruefen</c>: documents recognized automatically and possible duplicates. Everything can be
/// done here without opening the viewer. After each action the list is loaded again, the result is
/// announced with what is left, and focus goes to the next item (or the heading when none is left).
/// </summary>
public sealed partial class Review
{
    private PendingFocus _focus = default!;
    private LiveRegion? _status;
    private LiveRegion? _error;
    private ReviewItemDto[] _items = [];
    private ReviewItemDto? _deleting;
    private Guid? _editing;
    private string? _loadError;
    private bool _loading = true;

    // Loaded once for all "Herkunft unklar" sections of the list, not once per document; again only after a failure.
    private Task<ApiResult<HouseholdMemberDto[]>>? _household;

    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;

    [Inject] private ClassificationApi Api { get; set; } = default!;

    [Inject] private ReviewCounter Counter { get; set; } = default!;

    [Inject] private IJSRuntime Js { get; set; } = default!;

    [Inject] private ILogger<Review> Logger { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        _focus = new PendingFocus(Js);
        await LoadAsync();
    }

    protected override Task OnAfterRenderAsync(bool firstRender) => _focus.ApplyAsync();

    private async Task LoadAsync()
    {
        _loading = true;
        _loadError = null;
        var result = await Api.ListReviewAsync();
        _loading = false;
        if (!result.IsSuccess)
        {
            _loadError = result.Error;
            return;
        }

        _items = result.Value ?? [];
        if (_items.Any(item => item.Fields.NeedsOwnerCheck) && (_household is null || (_household.IsCompleted && _household is not { IsCompletedSuccessfully: true, Result.IsSuccess: true })))
        {
            _household = Api.ListHouseholdAsync();
        }

        Logger.ReviewLoaded(_items.Length);
        Counter.Set(_items.Length);
    }

    private string CountText(int count) => count == 1 ? L["ReviewCountOne"] : L["ReviewCount", count];

    private async Task ConfirmAsync(ReviewItemDto item)
    {
        var result = await Api.ConfirmAsync(item.Id, item.Fields.Recognition);
        if (result.IsConflict)
        {
            Logger.FieldsChangedMeanwhile();
            await ChangedMeanwhileAsync(item);
            return;
        }

        if (!result.IsSuccess)
        {
            await FailAsync(result.Error!, "review-edit-" + item.Id.ToString("N"));
            return;
        }

        Logger.FieldsConfirmed();
        await DoneAsync(item, "ReviewConfirmed");
    }

    private async Task KeepAsync(ReviewItemDto item)
    {
        var result = await Api.KeepDuplicateAsync(item.Id);
        if (!result.IsSuccess)
        {
            await FailAsync(result.Error!, "review-delete-" + item.Id.ToString("N"));
            return;
        }

        Logger.DuplicateKept();
        await DoneAsync(item, "ReviewKept");
    }

    private Task OwnerAnsweredAsync(ReviewItemDto item, OwnerCheckAnswer answer) => DoneAsync(
        item, answer.HandedOver ? "InpOwnerCheckHandedOver" : "InpOwnerCheckKept", extra: answer.ReceiverName);

    private void StartEdit(ReviewItemDto item) => _editing = item.Id;

    private void CancelEdit(ReviewItemDto item)
    {
        _editing = null;
        _focus.On("review-edit-" + item.Id.ToString("N"));
    }

    private async Task SavedAsync(ReviewItemDto item, string? newTitle)
    {
        _editing = null;
        await DoneAsync(item, "ReviewSaved", newTitle ?? item.Title);
    }

    // Recognized, or confirmed for the first time, since the list was loaded: load it again, say so (as an
    // error, so it interrupts) and go back to the item, the heading or "Erneut versuchen" (FocusAfterLoad).
    private async Task ChangedMeanwhileAsync(ReviewItemDto item)
    {
        _editing = null;
        await LoadAsync();
        var again = _items.FirstOrDefault(other => other.Id == item.Id);
        FocusAfterLoad(again);
        if (_error is not null)
        {
            // Without a reload the text must not claim one; the error box says why it failed.
            await _error.Announce(L[_loadError is null ? "ClsErrorChangedMeanwhile" : "ClsErrorConflict"]);
        }
    }

    private void AskDelete(ReviewItemDto item)
    {
        // The ConfirmBox takes focus itself (heading first, so the screen reader reads the question).
        _deleting = item;
    }

    private void CancelDelete()
    {
        if (_deleting is { } item)
        {
            _focus.On("review-delete-" + item.Id.ToString("N"));
        }

        _deleting = null;
    }

    private async Task DeleteAsync()
    {
        if (_deleting is not { } item)
        {
            return;
        }

        var result = await Api.DeleteDuplicateAsync(item.Id);
        _deleting = null;
        if (!result.IsSuccess)
        {
            await FailAsync(result.Error!, "review-delete-" + item.Id.ToString("N"));
            return;
        }

        Logger.DuplicateDeleted();
        await DoneAsync(item, "ReviewDeleted");
    }

    // Loads the list again (the item may be gone, or still there for its other reason) and says what is left.
    private async Task DoneAsync(ReviewItemDto item, string messageKey, string? title = null, string? extra = null)
    {
        var index = Array.FindIndex(_items, other => other.Id == item.Id);
        await LoadAsync();
        var message = L[messageKey, title ?? item.Title, extra ?? string.Empty].Value;
        if (_loadError is not null)
        {
            // The action worked, but what is left is unknown; the error box says why.
            await AnnounceAsync(message);
            FocusAfterLoad(null);
            return;
        }

        var left = _items.Length == 0 ? L["ReviewNoneLeft"].Value : CountText(_items.Length);
        await AnnounceAsync($"{message} {left}");

        var next = _items.FirstOrDefault(other => other.Id == item.Id)
            ?? (_items.Length == 0 ? null : _items[Math.Clamp(index, 0, _items.Length - 1)]);
        FocusAfterLoad(next);
    }

    // After loading the list again: the item, or the heading when none is left. When the list could not
    // be loaded, nothing of it is shown, so focus goes to "Erneut versuchen" next to the error.
    private void FocusAfterLoad(ReviewItemDto? item) =>
        _focus.On(_loadError is not null ? "review-retry" : item is null ? "review-heading" : "review-h-" + item.Id.ToString("N"));

    private async Task FailAsync(string message, string focusId)
    {
        _focus.On(focusId);
        if (_error is not null)
        {
            await _error.Announce(L["ErrorPrefix", message]);
        }
    }

    private async Task AnnounceAsync(string message)
    {
        if (_status is not null)
        {
            await _status.Announce(message);
        }
    }
}
