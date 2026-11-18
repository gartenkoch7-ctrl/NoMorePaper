namespace NoMorePaper.Infrastructure.Accounts;

/// <summary>
/// A password set through the administrator's reset link (table <c>admin_password_resets</c>).
/// Append-only: acknowledging the notice only fills <see cref="AcknowledgedAt"/>, rows are never
/// deleted while the account exists, so a later notice still lists every earlier reset.
/// </summary>
public sealed class AdminPasswordReset
{
    public required Guid Id { get; init; }

    public required Guid UserId { get; init; }

    public required DateTimeOffset ResetAt { get; init; }

    /// <summary>When the account confirmed the notice that listed this reset; <c>null</c> until then.</summary>
    public DateTimeOffset? AcknowledgedAt { get; set; }
}
