using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using NoMorePaper.Contracts.Trash;
using NoMorePaper.Web.Client.Accessibility;
using NoMorePaper.Web.Client.Documents;
using NoMorePaper.Web.Client.Localization;
using NoMorePaper.Web.Client.Trash;

namespace NoMorePaper.Web.Client.Pages;

/// <summary>
/// <c>/papierkorb</c>: what the account deleted. Restoring and deleting for good happen here without
/// leaving the page. After each action the list is loaded again, the result is announced, and focus
/// goes to the same button of the next entry (or the heading when none is left).
/// </summary>
public sealed partial class Trash
{
    private LiveRegion? _status;
    private LiveRegion? _error;
    private TrashItemDto[] _items = [];
    private TrashQuestion? _question;
    private string? _loadError;
    private bool _loading = true;
    private bool _busy;
    private string[] _focusIds = [];
    private IReadOnlyList<Design.BreadcrumbItem> _path = [];

    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;

    [Inject] private TrashApi Api { get; set; } = default!;

    [Inject] private IJSRuntime Js { get; set; } = default!;

    [Inject] private ILogger<Trash> Logger { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        _path = [new Design.BreadcrumbItem(L["NavSettings"], "einstellungen"), new Design.BreadcrumbItem(L["TrashTitle"], null)];
        await LoadAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_focusIds.Length == 0)
        {
            return;
        }

        // The table and the narrow list both hold the button; focusing the hidden one does nothing.
        var ids = _focusIds;
        _focusIds = [];
        foreach (var id in ids)
        {
            await Js.InvokeVoidAsync("nmpForms.focusById", id);
        }
    }

    private async Task LoadAsync()
    {
        _loadError = null;
        var result = await Api.ListAsync();
        _loading = false;
        if (!result.IsSuccess)
        {
            _loadError = result.Error;
            return;
        }

        _items = result.Value ?? [];
        Logger.TrashLoaded(_items.Length);
    }

    // "Erneut versuchen" disappears with the error, so focus must not stay on it.
    private async Task ReloadAsync()
    {
        _loading = true;
        await LoadAsync();
        _focusIds = TrashFocus.AfterReload(_loadError is not null);
    }

    private string KindText(TrashItemKindDto kind) => L[kind switch
    {
        TrashItemKindDto.Original => "TrashKindOriginal",
        TrashItemKindDto.Edited => "TrashKindEdited",
        _ => "TrashKindDocument",
    }].Value;

    private static string Local(DateTimeOffset at) => at.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

    private static string Iso(DateTimeOffset at) => at.ToString("o", CultureInfo.InvariantCulture);

    private void Cancel()
    {
        if (_question is not { } question)
        {
            return;
        }

        _question = null;
        _focusIds = TrashFocus.AfterCancel(question);
    }

    // The question opens with its own focus (heading first), so no focus is requested here.
    private void AskEmpty() => _question = new TrashQuestion(TrashQuestionKind.Empty, Guid.Empty);

    private Task PurgeClicked(TrashItemDto item)
    {
        _question = new TrashQuestion(TrashQuestionKind.Purge, item.Id);
        return Task.CompletedTask;
    }

    private async Task RestoreAsync(TrashItemDto item)
    {
        if (item.RestoreSwaps)
        {
            _question = new TrashQuestion(TrashQuestionKind.Swap, item.Id);
            return;
        }

        await RunRestoreAsync(item);
    }

    private async Task ConfirmAsync()
    {
        if (_question is not { } question || _busy)
        {
            return;
        }

        var item = _items.FirstOrDefault(other => other.Id == question.Id);
        _question = null;
        if (question.Kind == TrashQuestionKind.Empty)
        {
            await EmptyAsync();
        }
        else if (item is not null)
        {
            await (question.Kind == TrashQuestionKind.Swap ? RunRestoreAsync(item) : RunPurgeAsync(item));
        }
    }

    private Task RunRestoreAsync(TrashItemDto item) => RunAsync(
        () => Api.RestoreAsync(item.Id),
        TrashFocus.Button(TrashFocus.RestoreAction, item.Id),
        () =>
        {
            Logger.TrashItemRestored();
            var message = L[item.RestoreSwaps ? "TrashRestoredSwapped" : "TrashRestored", item.Title].Value;
            return DoneAsync(item, message, TrashFocus.RestoreAction);
        });

    private Task RunPurgeAsync(TrashItemDto item) => RunAsync(
        () => Api.PurgeAsync(item.Id),
        TrashFocus.Button(TrashFocus.PurgeAction, item.Id),
        () =>
        {
            Logger.TrashItemPurged();
            return DoneAsync(item, L["TrashPurged", item.Title].Value, TrashFocus.PurgeAction);
        });

    private Task EmptyAsync()
    {
        var count = _items.Length;
        return RunAsync(
            () => Api.EmptyAsync(),
            [TrashFocus.EmptyButton],
            async () =>
            {
                Logger.TrashEmptied(count);
                await LoadAsync();
                await AnnounceAsync(L["TrashEmptied"].Value);
                _focusIds = TrashFocus.AfterReload(_loadError is not null);
            });
    }

    // One action at a time. An error of an earlier action must not stay on screen next to the result of this one.
    private async Task RunAsync(Func<Task<ApiResult<bool>>> call, string[] triggerIds, Func<Task> done)
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        _error?.Clear();
        try
        {
            var result = await call();
            await (result.IsSuccess ? done() : FailAsync(result, triggerIds));
        }
        finally
        {
            _busy = false;
        }
    }

    // Loads the list again and says what happened; focus goes to the same button of the entry that
    // took the place of the one acted on, or to the heading when nothing is left.
    private async Task DoneAsync(TrashItemDto item, string message, string action)
    {
        var index = Array.FindIndex(_items, other => other.Id == item.Id);
        await LoadAsync();
        await AnnounceAsync(message);
        _focusIds = TrashFocus.AfterAction(_items, index, action, _loadError is not null);
    }

    private async Task FailAsync(ApiResult<bool> result, string[] triggerIds)
    {
        Logger.TrashActionFailed(result.IsConflict ? 409 : 0);
        if (result.IsConflict)
        {
            // The entry or its document changed meanwhile: show what is there now.
            await LoadAsync();
            _focusIds = TrashFocus.AfterReload(_loadError is not null);
        }
        else
        {
            _focusIds = triggerIds;
        }

        // A failed reload says so itself (TrashLoadError); the conflict text would claim the list was reloaded.
        if (_error is not null && !(result.IsConflict && _loadError is not null))
        {
            await _error.Announce(result.IsConflict ? result.Error! : L["TrashActionFailed", result.Error!].Value);
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
