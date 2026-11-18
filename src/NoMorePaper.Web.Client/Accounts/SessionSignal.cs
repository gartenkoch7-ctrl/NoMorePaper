namespace NoMorePaper.Web.Client.Accounts;

/// <summary>
/// Carries "the server said 401" from the HTTP pipeline to <see cref="AuthState"/>. A separate
/// object because the HTTP client is built before the services that use it.
/// </summary>
public sealed class SessionSignal
{
    public event Action? Expired;

    public void RaiseExpired() => Expired?.Invoke();
}
