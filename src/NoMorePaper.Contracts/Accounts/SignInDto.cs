namespace NoMorePaper.Contracts.Accounts;

/// <summary>Body of <c>POST /api/auth/login</c>.</summary>
public sealed record SignInDto(string UserName, string Password);
