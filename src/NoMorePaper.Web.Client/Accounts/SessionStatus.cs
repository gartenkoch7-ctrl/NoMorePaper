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
