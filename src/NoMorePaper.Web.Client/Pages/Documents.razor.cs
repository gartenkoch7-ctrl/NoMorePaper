using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using NoMorePaper.Contracts.Classification;
using NoMorePaper.Contracts.Documents;
using NoMorePaper.Web.Client.Accessibility;
using NoMorePaper.Web.Client.Classification;
using NoMorePaper.Web.Client.Documents;
using NoMorePaper.Web.Client.Localization;

namespace NoMorePaper.Web.Client.Pages;

/// <summary>
/// <c>/dokumente</c>: the documents the account may see, filtered, page by page, as table or tiles. The
/// counts (header, type chips, "of N") come from a second call; when it fails the page still works without
/// the total. A filter or a type chip starts again at page 1.
/// </summary>
public sealed partial class Documents : IDisposable
{
    private static readonly int[] PageSizes = [10, 25, 50, 100];

    private readonly CancellationTokenSource _cts = new();
    private DocumentSummaryDto[] _documents = [];
    private DocumentCountsDto? _counts;
    private DocumentFilterDto? _barFilter;
    private Guid? _typeId;
    private int _page = 1;
    private int _pageSize = 50;
    private DocumentListView _view;
    private LiveRegion? _changes;
    private LiveRegion? _count;
    private string? _error;
    private bool _loading = true;

    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;

    [Inject] private DocumentApi Api { get; set; } = default!;

    [Inject] private ILogger<Documents> Logger { get; set; } = default!;

    private DocumentFilterDto? Combined => DocumentFilterQuery.Combine(_barFilter, _typeId);

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync(announceChanges: false);
        await LoadCountsAsync();
        _loading = false;
        _ = PollAsync();
    }

    private async Task ReloadAsync()
    {
        _loading = true;
        await LoadAsync(announceChanges: false);
        await LoadCountsAsync();
        _loading = false;
        _ = PollAsync();
    }

    private Task ApplyFilterAsync(DocumentFilterDto bar)
    {
        _barFilter = DocumentFilterQuery.ActiveCount(bar) == 0 ? null : bar;
        return ReloadForFilterAsync();
    }

    private Task SelectTypeAsync(Guid? typeId)
    {
        _typeId = typeId;
        return ReloadForFilterAsync();
    }

    // A new filter replaces the list, so the old state changes are not "changes" any more; the number of
    // documents found is announced, because the list changes silently for a screen reader.
    private async Task ReloadForFilterAsync()
    {
        _page = 1;
        _lastStates = [];
        await LoadAsync(announceChanges: false);
        await LoadCountsAsync();
        var found = _counts?.Total ?? _documents.Length;
        var active = DocumentFilterQuery.ActiveCount(Combined);
        Logger.FilterApplied(active, found);
        if (_count is not null)
        {
            await _count.Announce(_error is not null ? L["DocumentsLoadError", _error]
                : found == 0 ? L["FilterFoundNone"]
                : found == 1 ? L["FilterFoundOne"] : L["FilterFound", found]);
        }

        _ = PollAsync();
    }

    private async Task ChangePageAsync(int page)
    {
        _page = Math.Max(page, 1);
        await LoadAsync(announceChanges: false);
        Logger.ListPageChanged(_page, _pageSize);
        if (_count is not null && _error is null)
        {
            await _count.Announce(RangeText());
        }

        _ = PollAsync();
    }

    private async Task ChangePageSizeAsync(int size)
    {
        if (!PageSizes.Contains(size))
        {
            return;
        }

        _pageSize = size;
        await ChangePageAsync(1);
    }

    private void SetView(DocumentListView view)
    {
        if (view == _view)
        {
            return;
        }

        _view = view;
        Logger.ListViewChanged(view == DocumentListView.Table ? "table" : "tiles");
    }

    private async Task LoadAsync(bool announceChanges)
    {
        var result = await Api.ListAsync(_page, _pageSize, Combined, _cts.Token);
        if (!result.IsSuccess)
        {
            _error = result.Error;
            return;
        }

        _error = null;
        _documents = result.Value ?? [];
        Logger.DocumentListLoaded(_documents.Length);
        if (_documents.Length == 0 && _page > 1)
        {
            // The documents of this page are gone (deleted or filtered away): start again at the first page.
            _page = 1;
            await LoadAsync(announceChanges: false);
            return;
        }

        if (announceChanges)
        {
            await AnnounceChangesAsync();
        }

        _lastStates = _documents.ToDictionary(d => d.Id, d => d.State);
    }

    // No error message when this fails: the page works without the total ("page 2", previous/next).
    private async Task LoadCountsAsync()
    {
        var result = await Api.CountAsync(Combined, _cts.Token);
        _counts = result.IsSuccess ? result.Value : null;
        if (_counts is not null)
        {
            Logger.DocumentCountsLoaded(_counts.Total, _counts.ByType.Count);
        }
    }

    private string RangeText()
    {
        if (_counts is null)
        {
            return L["DocListPage", _page];
        }

        var (from, to) = PageWindow.Range(_page, _pageSize, _counts.Total);
        return L["DocListRange", from, to, _counts.Total];
    }
}
