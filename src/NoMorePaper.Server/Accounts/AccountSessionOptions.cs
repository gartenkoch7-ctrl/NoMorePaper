namespace NoMorePaper.Server.Accounts;

/// <summary>How sessions are kept (docs/plan/m2-konten.md).</summary>
internal sealed class AccountSessionOptions
{
    /// <summary>Signed in for this long after the last visit (sliding).</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    /// <summary>
    /// How often a session is checked against the store: a removed account or "sign out everywhere"
    /// ends other sessions within this time. Tests set it to zero.
    /// </summary>
    public TimeSpan ValidationInterval { get; set; } = TimeSpan.FromMinutes(5);
}
