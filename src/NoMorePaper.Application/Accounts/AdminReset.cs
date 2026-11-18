namespace NoMorePaper.Application.Accounts;

/// <summary>A password set through the administrator's reset link, as the store records it (never deleted).</summary>
/// <param name="AcknowledgedAt">When the account confirmed a notice that listed it; <c>null</c> until then.</param>
public sealed record AdminReset(DateTimeOffset ResetAt, DateTimeOffset? AcknowledgedAt);
