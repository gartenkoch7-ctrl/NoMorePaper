namespace NoMorePaper.Contracts.Accounts;

/// <summary>An account: the signed-in one (<c>GET /api/auth/me</c>) or a member (<c>GET /api/members</c>).</summary>
/// <param name="UserName">Name for signing in.</param>
/// <param name="DisplayName">Name shown to the household.</param>
/// <param name="IsAdmin">Set up the household and manages its members.</param>
public sealed record AccountDto(Guid Id, string UserName, string DisplayName, bool IsAdmin);
