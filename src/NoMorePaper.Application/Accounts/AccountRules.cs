namespace NoMorePaper.Application.Accounts;

/// <summary>Limits of the account feature (docs/plan/m2-konten.md); the store configures Identity from them.</summary>
public static class AccountRules
{
    /// <summary>The only password rule: at least this many characters, no character classes.</summary>
    public const int MinPasswordLength = 10;

    /// <summary>Failed sign-ins in a row that lock an account for <see cref="LockoutDuration"/>.</summary>
    public const int MaxFailedSignIns = 5;

    public const int MaxUserNameLength = 64;

    public const int MaxDisplayNameLength = 100;

    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(5);

    /// <summary>An invitation link is valid this long, and only once.</summary>
    public static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);

    /// <summary>A password reset link is valid this long, and only once.</summary>
    public static readonly TimeSpan ResetLinkLifetime = TimeSpan.FromHours(24);

    /// <summary>When a link created at <paramref name="now"/> expires, in whole seconds: what is shown is exactly what is stored.</summary>
    public static DateTimeOffset Expiry(DateTimeOffset now, TimeSpan lifetime)
    {
        var end = now.ToUniversalTime() + lifetime;
        return end.AddTicks(-(end.Ticks % TimeSpan.TicksPerSecond));
    }
}
