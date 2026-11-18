using Microsoft.Extensions.Logging;
using NoMorePaper.Contracts.Accounts;

namespace NoMorePaper.Web.Client.Accounts;

/// <summary>What the browser knows about its session.</summary>
public enum SessionStatus
{
    /// <summary>Not asked yet.</summary>
    Unknown,
    SignedIn,
    SignedOut,

    /// <summary>No account exists yet: the household must be set up first.</summary>
    SetupNeeded,

    /// <summary>The server could not be asked (offline, error).</summary>
    Unavailable,
}

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

    public AuthState(AccountApi api, SessionSignal signal, ILogger<AuthState> logger)
    {
        _api = api;
        _signal = signal;
        _logger = logger;
        _signal.Expired += OnExpired;
    }

    public event Action? Changed;

    public SessionStatus Status { get; private set; }

    public MeDto? Me { get; private set; }

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

    private void Set(SessionStatus status, MeDto? me)
    {
        Status = status;
        Me = me;
        _logger.SessionLoaded(status);
        Changed?.Invoke();
    }

    public void Dispose() => _signal.Expired -= OnExpired;
}
