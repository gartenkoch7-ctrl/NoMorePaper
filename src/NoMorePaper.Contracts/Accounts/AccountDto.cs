namespace NoMorePaper.Contracts.Accounts;

/// <summary>An account: the signed-in one (<c>GET /api/auth/me</c>) or a member (<c>GET /api/members</c>).</summary>
/// <param name="UserName">Name for signing in.</param>
/// <param name="DisplayName">Name shown to the household.</param>
/// <param name="IsAdmin">Set up the household and manages its members.</param>
/// <param name="AdminResetAt">
/// Only in <c>GET /api/auth/me</c>: when a reset link from the administrator set the password, until
/// the account confirms the notice with <c>POST /api/account/acknowledge-reset</c>; otherwise <c>null</c>.
/// </param>
public sealed record AccountDto(Guid Id, string UserName, string DisplayName, bool IsAdmin, DateTimeOffset? AdminResetAt = null);
