namespace NoMorePaper.Contracts.Accounts;

/// <summary>Credentials for signing in.</summary>
public sealed record LoginRequestDto(string UserName, string Password);
