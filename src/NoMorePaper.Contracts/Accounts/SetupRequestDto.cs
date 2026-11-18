namespace NoMorePaper.Contracts.Accounts;

/// <summary>Creates the administrator account of the household.</summary>
public sealed record SetupRequestDto(string UserName, string DisplayName, string Password);
