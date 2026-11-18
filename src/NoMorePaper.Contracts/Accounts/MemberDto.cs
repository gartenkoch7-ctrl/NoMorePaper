namespace NoMorePaper.Contracts.Accounts;

/// <summary>One member of the household.</summary>
public sealed record MemberDto(Guid Id, string UserName, string DisplayName, bool IsAdmin);
