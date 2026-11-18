namespace NoMorePaper.Contracts.Accounts;

/// <summary>Body of <c>POST /api/password-reset/{token}</c>.</summary>
public sealed record NewPasswordDto(string NewPassword);
