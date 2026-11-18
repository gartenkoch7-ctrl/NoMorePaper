using Microsoft.Extensions.Logging;
using NoMorePaper.Contracts.Accounts;

namespace NoMorePaper.Web.Client.Accounts;

/// <summary>
/// The signed-in account for all pages and the layout. Loaded once on demand, refreshed after
/// sign-in, and reset when the server answers 401. <see cref="Changed"/> lets the layout and the
/// sign-in guard react.
/// </summary>
public sealed class AuthState : IDisposable
{
    private readonly AccountApi _api;
    private readonly SessionSignal _signal;
    private readonly ILogger<AuthState> _logger;
    private Task? _loading;
    private SignedInAccount? _me;

    public AuthState(AccountApi api, SessionSignal signal, ILogger<AuthState> logger)
    {
        _api = api;
        _signal = signal;
        _logger = logger;
        _signal.Expired += OnExpired;
    }

    public event Action? Changed;

    public SessionStatus Status { get; private set; }

    public AccountDto? Me => _me?.Account;

    /// <summary>Resets by the administrator's link not confirmed yet, oldest first; empty when there is none.</summary>
    public IReadOnlyList<DateTimeOffset> AdminResets => _me?.AdminResets ?? [];

    public bool IsSignedIn => Status == SessionStatus.SignedIn;

    public bool IsAdmin => Me?.IsAdmin == true;

    /// <summary>Loads the session once; later callers wait for the same load.</summary>
    public Task EnsureLoadedAsync() => _loading ??= RefreshAsync();

    public async Task RefreshAsync()
    {
        var me = await _api.GetMeAsync();
        if (me.IsSuccess)
        {
            Set(SessionStatus.SignedIn, me.Value);
            return;
        }

        var setup = await _api.GetSetupAsync();
        var status = setup.IsSuccess
            ? (setup.Value!.Needed ? SessionStatus.SetupNeeded : SessionStatus.SignedOut)
            : SessionStatus.Unavailable;
        Set(status, null);
    }

    /// <summary>The notice about the administrator's reset was confirmed: forget it without asking the server.</summary>
    public void MarkAdminResetSeen()
    {
        if (_me is { } me && (me.AdminResets.Count > 0 || me.Account.AdminResetAt is not null))
        {
            _me = me with { Account = me.Account with { AdminResetAt = null }, AdminResets = [] };
            Changed?.Invoke();
        }
    }

    /// <summary>After sign-out (or 401): forget the account without asking the server.</summary>
    public void MarkSignedOut() => Set(SessionStatus.SignedOut, null);

    private void OnExpired()
    {
        _logger.SessionExpired();
        if (Status == SessionStatus.SignedIn)
        {
            MarkSignedOut();
        }
    }

    private void Set(SessionStatus status, SignedInAccount? me)
    {
        Status = status;
        _me = me;
        _logger.SessionLoaded(status);
        Changed?.Invoke();
    }

    public void Dispose() => _signal.Expired -= OnExpired;
}
