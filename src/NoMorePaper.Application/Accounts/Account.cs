namespace NoMorePaper.Application.Accounts;

/// <summary>A member of the household as the store reports it.</summary>
/// <param name="SecurityStamp">Changes whenever all sessions of the account must end (password, sign-out everywhere).</param>
public sealed record Account(Guid Id, string UserName, string DisplayName, bool IsAdmin, string SecurityStamp);
