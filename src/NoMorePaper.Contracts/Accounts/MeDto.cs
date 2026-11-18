namespace NoMorePaper.Contracts.Accounts;

/// <summary>The signed-in account.</summary>
public sealed record MeDto(Guid Id, string UserName, string DisplayName, bool IsAdmin);
