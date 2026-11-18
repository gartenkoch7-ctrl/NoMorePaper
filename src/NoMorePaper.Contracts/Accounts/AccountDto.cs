namespace NoMorePaper.Contracts.Accounts;

/// <summary>An account: the signed-in one (<c>GET /api/auth/me</c>) or a member (<c>GET /api/members</c>).</summary>
/// <param name="UserName">Name for signing in.</param>
/// <param name="DisplayName">Name shown to the household.</param>
/// <param name="IsAdmin">Set up the household and manages its members.</param>
/// <param name="AdminResetAt">
/// Only in <c>GET /api/auth/me</c>: the oldest reset through the administrator's reset link that the
/// account has not confirmed yet (<c>POST /api/account/acknowledge-reset</c>); otherwise <c>null</c>.
/// Set exactly when <paramref name="AdminResets"/> is not empty.
/// </param>
/// <param name="AdminResets">
/// Only in <c>GET /api/auth/me</c>: while a reset is unconfirmed, every reset through the
/// administrator's reset link, oldest first, including earlier confirmed ones (they may have been
/// confirmed by somebody else); empty when there is nothing to tell. <c>null</c> for members.
/// </param>
public sealed record AccountDto(
    Guid Id,
    string UserName,
    string DisplayName,
    bool IsAdmin,
    DateTimeOffset? AdminResetAt = null,
    IReadOnlyList<DateTimeOffset>? AdminResets = null);
