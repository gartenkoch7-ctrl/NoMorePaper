namespace NoMorePaper.Application.Accounts;

/// <summary>A member of the household as the store reports it.</summary>
/// <param name="SecurityStamp">Changes whenever all sessions of the account must end (password, sign-out everywhere).</param>
/// <param name="AdminResetAt">
/// When a reset link from the administrator last set the password, until the member has seen the
/// notice about it (<see cref="AcknowledgeAdminReset"/>); <c>null</c> otherwise.
/// </param>
public sealed record Account(
    Guid Id, string UserName, string DisplayName, bool IsAdmin, string SecurityStamp, DateTimeOffset? AdminResetAt = null);
