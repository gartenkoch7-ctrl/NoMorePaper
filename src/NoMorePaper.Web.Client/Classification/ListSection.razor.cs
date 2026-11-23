using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using NoMorePaper.Contracts.Classification;
using NoMorePaper.Web.Client.Accessibility;
using NoMorePaper.Web.Client.Localization;

namespace NoMorePaper.Web.Client.Classification;

/// <summary>Code of <see cref="ListSection"/>: loading, renaming and merging, each result announced.</summary>
public sealed partial class ListSection
{
    private readonly HashSet<Guid> _selected = [];
    private PendingFocus _focus = default!;
    private LiveRegion? _status;
    private LiveRegion? _error;
    private EntryDto[] _entries = [];
    private EntryDto? _renaming;
    // New key per ask, so a re-ask re-creates the box and it takes focus again.
    private int _mergeRound;
    private string _newName = string.Empty;
    private string _target = string.Empty;
    private string? _loadError;
    private bool _loading = true;
    private bool _merging;

    [Parameter, EditorRequired] public EntryKind Kind { get; set; }

    [Parameter, EditorRequired] public string Heading { get; set; } = string.Empty;

    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;

    [Inject] private ClassificationApi Api { get; set; } = default!;

    [Inject] private IJSRuntime Js { get; set; } = default!;

    private string Prefix => "list-" + EntryKinds.Route(Kind);

    private string RowId(string action, EntryDto entry) => $"{Prefix}-{action}-{entry.Id:N}";

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
        var result = await Api.ListEntriesAsync(Kind);
        _loading = false;
        if (!result.IsSuccess)
        {
            _loadError = result.Error;
            return;
        }

        _entries = [.. (result.Value ?? []).OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)];
        _selected.IntersectWith(_entries.Select(entry => entry.Id));
    }

    private void Toggle(Guid id, bool on)
    {
        // A merge question was about the old selection.
        _merging = false;
        if (on)
        {
            _selected.Add(id);
        }
        else
        {
            _selected.Remove(id);
        }
    }

    private void AskRename(EntryDto entry)
    {
        // The ConfirmBox focuses the name field itself (InitialFocusId).
        (_renaming, _newName, _merging) = (entry, entry.Name, false);
    }

    private void CancelRename()
    {
        if (_renaming is { } entry)
        {
            _focus.On(RowId("rename", entry));
        }

        _renaming = null;
    }

    // Enter saves only inside the field; Escape is handled by the ConfirmBox around it.
    private async Task OnRenameInputKey(KeyboardEventArgs args)
    {
        if (args.Key == "Enter")
        {
            await RenameAsync();
        }
    }

    private async Task RenameAsync()
    {
        if (_renaming is not { } entry)
        {
            return;
        }

        var name = _newName.Trim();
        if (name.Length == 0)
        {
            await FailAsync(L["ErrorRequired", L["ListsNewName"]], Prefix + "-rename-input");
            return;
        }

        var result = await Api.RenameEntryAsync(Kind, entry.Id, name);
        if (!result.IsSuccess)
        {
            await FailAsync(result.Error!, Prefix + "-rename-input");
            return;
        }

        _renaming = null;
        await LoadAsync();
        _focus.On(RowId("rename", entry));
        await AnnounceAsync(L["ListsRenamed", entry.Name, name]);
    }

    private async Task AskMerge()
    {
        if (_selected.Count < 2)
        {
            await FailAsync(L["ListsMergeTooFew"], Prefix + "-merge");
            return;
        }

        (_merging, _renaming) = (true, null);
        _mergeRound++;
        _target = _entries.First(entry => _selected.Contains(entry.Id)).Id.ToString();
    }

    private void CancelMerge()
    {
        _merging = false;
        _focus.On(Prefix + "-merge");
    }

    private async Task MergeAsync()
    {
        if (!Guid.TryParse(_target, out var target) || !_selected.Contains(target))
        {
            return;
        }

        var sources = _selected.Where(id => id != target).ToArray();
        var targetEntry = _entries.First(entry => entry.Id == target);
        var result = await Api.MergeEntriesAsync(Kind, target, sources);
        if (!result.IsSuccess)
        {
            _merging = false;
            await FailAsync(result.Error!, Prefix + "-merge");
            return;
        }

        _merging = false;
        _selected.Clear();
        await LoadAsync();
        _focus.On(RowId("rename", targetEntry));
        await AnnounceAsync(L["ListsMerged", sources.Length + 1, targetEntry.Name]);
    }

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
        _error?.Clear();
        if (_status is not null)
        {
            await _status.Announce(message);
        }
    }
}
