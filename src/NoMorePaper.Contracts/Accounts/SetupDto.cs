namespace NoMorePaper.Contracts.Accounts;

/// <summary>Body of <c>POST /api/setup</c>: the administrator. The password needs at least 10 characters.</summary>
public sealed record SetupDto(string UserName, string DisplayName, string Password);
