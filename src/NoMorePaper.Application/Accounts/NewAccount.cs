namespace NoMorePaper.Application.Accounts;

/// <summary>Input for a new account, trimmed and checked by <see cref="AccountInput"/>; the store checks the password.</summary>
public sealed record NewAccount(string UserName, string DisplayName, string Password);
