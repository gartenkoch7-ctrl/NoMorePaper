using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using NoMorePaper.Contracts.Classification;
using NoMorePaper.Web.Client.Accessibility;
using NoMorePaper.Web.Client.Accounts;
using NoMorePaper.Web.Client.Localization;

namespace NoMorePaper.Web.Client.Classification;

/// <summary>Code of <see cref="DocumentFieldsEditor"/>: loading the lists, validating and saving the fields.</summary>
public sealed partial class DocumentFieldsEditor
{
    private readonly List<EntryDto> _correspondents = [];
    private readonly List<EntryDto> _types = [];
    private readonly List<EntryDto> _allTags = [];
    private FormValidation _form = default!;
    private PendingFocus _focus = default!;
    private LiveRegion? _error;
    private EntryDto? _correspondent;
    private EntryDto? _type;
    private IReadOnlyList<EntryDto> _tags = [];
    private string _title = string.Empty;
    private DateOnly? _date;
    private string _amountText = string.Empty;
    private string _currency = DefaultCurrency;
    private string _summary = string.Empty;
    private string? _loadError;
    private bool _loading = true;
    private bool _busy;

    private const string DefaultCurrency = "EUR";

    [Parameter, EditorRequired] public Guid DocumentId { get; set; }

    [Parameter, EditorRequired] public string DocumentTitle { get; set; } = string.Empty;

    [Parameter, EditorRequired] public DocumentFieldsDto Fields { get; set; } = default!;

    /// <summary>Makes the element ids unique when several editors are on one page.</summary>
    [Parameter] public string Prefix { get; set; } = "fields";

    /// <summary>The fields were saved (and so confirmed); the new title when it changed, otherwise <c>null</c>.</summary>
    [Parameter] public EventCallback<string?> OnSaved { get; set; }

    [Parameter] public EventCallback OnCancel { get; set; }

    /// <summary>Saving was refused because the fields changed meanwhile (409); the page loads them again and says so.</summary>
    [Parameter] public EventCallback OnConflict { get; set; }

    protected override async Task OnInitializedAsync()
    {
        _form = new FormValidation(L, Js);
        _focus = new PendingFocus(Js);
        (_correspondent, _type, _tags) = (Fields.Correspondent, Fields.DocumentType, [.. Fields.Tags]);
        _title = DocumentTitle;
        _date = Fields.DocumentDate;
        _summary = Fields.Summary ?? string.Empty;
        if (Fields.Amount is { } amount)
        {
            (_amountText, _currency) = (FieldFormat.AmountInput(amount.Value, CultureInfo.CurrentCulture), amount.Currency);
        }

        await LoadAsync();
    }

    protected override Task OnAfterRenderAsync(bool firstRender) => _focus.ApplyAsync();

    private async Task LoadAsync()
    {
        _loading = true;
        _loadError = null;
        var lists = new (EntryKind Kind, List<EntryDto> Target)[]
        {
            (EntryKind.Correspondents, _correspondents), (EntryKind.DocumentTypes, _types), (EntryKind.Tags, _allTags),
        };
        foreach (var (kind, target) in lists)
        {
            var result = await Api.ListEntriesAsync(kind);
            if (!result.IsSuccess)
            {
                _loadError = result.Error;
                _loading = false;
                return;
            }

            target.Clear();
            target.AddRange(result.Value ?? []);
        }

        _loading = false;
        _focus.On(Prefix + "-title");
    }

    private Func<string, Task<EntryDto?>> CreateFor(EntryKind kind, List<EntryDto> list) => async name =>
    {
        var result = await Api.CreateEntryAsync(kind, name);
        if (result.Value is not { } entry)
        {
            return null;
        }

        if (list.All(existing => existing.Id != entry.Id))
        {
            list.Add(entry);
        }

        return entry;
    };

    private Task TagsChangedAsync(IReadOnlyList<EntryDto> tags)
    {
        _tags = tags;
        return Task.CompletedTask;
    }

    private async Task SaveAsync()
    {
        _form.Reset();
        MoneyDto? amount = null;
        if (_amountText.Trim().Length > 0)
        {
            var currency = FieldFormat.NormalizeCurrency(_currency);
            if (!FieldFormat.TryParseAmount(_amountText, CultureInfo.CurrentCulture, out var value))
            {
                _form.Set(Prefix + "-amount", L["ClsErrorAmount"]);
            }
            else if (currency is null)
            {
                _form.Set(Prefix + "-currency", L["ClsErrorCurrency"]);
            }
            else
            {
                amount = new MoneyDto(value, currency);
            }
        }

        if (await _form.ReportAsync(_error))
        {
            return;
        }

        var newTitle = _title.Trim();
        var request = new EditFieldsRequestDto(
            newTitle.Length == 0 ? null : newTitle,
            _correspondent?.Id,
            _type?.Id,
            _date,
            amount,
            _summary.Trim().Length == 0 ? null : _summary.Trim(),
            [.. _tags.Select(tag => tag.Id)],
            Fields.Recognition);
        _busy = true;
        var result = await Api.EditFieldsAsync(DocumentId, request);
        _busy = false;
        if (!result.IsSuccess)
        {
            // Recognized, or confirmed for the first time, since the form was opened: the page loads the
            // fields again instead of overwriting values nobody has seen.
            if (result.IsConflict && OnConflict.HasDelegate)
            {
                Logger.FieldsChangedMeanwhile();
                await OnConflict.InvokeAsync();
                return;
            }

            if (_error is not null)
            {
                await _error.Announce(L["ErrorPrefix", result.Error!]);
            }

            return;
        }

        Logger.FieldsSaved();
        await OnSaved.InvokeAsync(request.Title is { } title && title != DocumentTitle ? title : null);
    }

    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;

    [Inject] private ClassificationApi Api { get; set; } = default!;

    [Inject] private IJSRuntime Js { get; set; } = default!;

    [Inject] private ILogger<DocumentFieldsEditor> Logger { get; set; } = default!;
}
