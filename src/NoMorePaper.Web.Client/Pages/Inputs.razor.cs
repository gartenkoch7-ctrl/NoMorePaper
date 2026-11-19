using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using NoMorePaper.Contracts.Inputs;
using NoMorePaper.Web.Client.Accessibility;
using NoMorePaper.Web.Client.Accounts;
using NoMorePaper.Web.Client.Documents;
using NoMorePaper.Web.Client.Inputs;
using NoMorePaper.Web.Client.Localization;

namespace NoMorePaper.Web.Client.Pages;

/// <summary>
/// <c>/eingaenge</c>: whether each input runs, when it last ran and what is wrong, in words. The page asks
/// again every minute and says in a live region what changed; "Jetzt aktualisieren" does it by hand.
/// </summary>
public sealed partial class Inputs : IDisposable
{
    private static readonly TimeSpan _interval = TimeSpan.FromSeconds(60);

    private readonly CancellationTokenSource _stop = new();
    private LiveRegion? _status;
    private InputStatusDto[]? _rows;
    private Dictionary<string, string> _seen = [];
    private DateTimeOffset _now;
    private string? _loadError;
    private bool _loading;

    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;

    [Inject] private InputsApi Api { get; set; } = default!;

    [Inject] private AuthState Auth { get; set; } = default!;

    [Inject] private TimeProvider Clock { get; set; } = default!;

    [Inject] private ILogger<Inputs> Logger { get; set; } = default!;

    /// <summary>
    /// Whether the page <c>/eingaenge/icloud</c> exists. Until track D builds it (after the test with a real
    /// Apple ID, ADR-0018) the iCloud section says so in words instead of linking to a page that is not there;
    /// track D sets this to <c>true</c>.
    /// </summary>
    internal static bool ICloudPageExists => false;

    private bool MailNotConfigured => RowsOf("mail").Any(row => row.Problem == "not-configured") || !HasRows("mail");

    // Only the signed-in account's own row can be renewed here; the name is all the row carries.
    private bool MyCodeNeeded => RowsOf("icloud").Any(row => row.Problem == "code-needed" && row.AccountName == Auth.Me?.DisplayName);

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync(byHand: false);
        _ = RefreshEveryMinuteAsync(_stop.Token);
    }

    private IEnumerable<InputStatusDto> RowsOf(string kind) => (_rows ?? []).Where(row => row.Kind == kind);

    private bool HasRows(string kind) => RowsOf(kind).Any();

    private async Task RefreshEveryMinuteAsync(CancellationToken stop)
    {
        using var timer = new PeriodicTimer(_interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stop))
            {
                await InvokeAsync(() => LoadAsync(byHand: false));
            }
        }
        catch (OperationCanceledException)
        {
            // The page was left.
        }
    }

    private Task RefreshByHandAsync() => LoadAsync(byHand: true);

    // After the first load only changes are announced; by hand the answer is always spoken, even "nothing new".
    private async Task LoadAsync(bool byHand)
    {
        // The button stays enabled (a disabled button loses the keyboard focus); a second ask while one runs is ignored.
        if (_loading)
        {
            return;
        }

        _loading = true;
        ApiResult<InputStatusDto[]> result;
        try
        {
            result = await Api.ListAsync(_stop.Token);
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
            return; // The page was left while the answer was on its way.
        }
        finally
        {
            _loading = false;
        }

        if (!result.IsSuccess || result.Value is null)
        {
            // A new error box is an alert and speaks for itself; stale rows would only mislead. The same error
            // again leaves the box unchanged, and an unchanged alert says nothing: by hand it is said here.
            var before = _loadError;
            _loadError = result.Error ?? L["ErrorUnreachable"];
            Logger.InputsPageFailed("load-inputs");
            StateHasChanged();
            if (byHand && before == _loadError)
            {
                await AnnounceAsync(L["InpLoadError", _loadError]);
            }

            return;
        }

        var first = _rows is null;
        _loadError = null;
        _rows = result.Value;
        _now = Clock.GetUtcNow();
        StateHasChanged();
        if (first)
        {
            Logger.InputsPageLoaded(_rows.Length);
        }

        var seen = _rows.ToDictionary(Key, Signature);
        var changed = _rows.Where(row => !_seen.TryGetValue(Key(row), out var before) || before != seen[Key(row)]).ToArray();
        _seen = seen;
        if (first)
        {
            return;
        }

        if (changed.Length > 0)
        {
            await AnnounceAsync(L["InpChanged", string.Join(" ", changed.Select(Describe))]);
        }
        else if (byHand)
        {
            await AnnounceAsync(L["InpNothingNew"]);
        }
    }

    private static string Key(InputStatusDto row) => row.Kind + "|" + row.AccountName;

    // Times change with every run and are left out: only what the person has to know counts as a change.
    private static string Signature(InputStatusDto row) => $"{row.State}|{row.Problem}|{row.NeedsAttention}|{row.LastRunAt is not null}";

    private string Describe(InputStatusDto row)
    {
        var name = row.AccountName is null ? InputTexts.Kind(L, row.Kind) : $"{InputTexts.Kind(L, row.Kind)} ({row.AccountName})";
        var state = row.Problem is null ? L["InpStateOk"].Value : L["InpStateProblem", InputTexts.Problem(L, row.Problem)].Value;
        return $"{name}: {state}";
    }

    private async Task AnnounceAsync(string message)
    {
        if (_status is not null)
        {
            await _status.Announce(message);
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
    }
}
