namespace NoMorePaper.Contracts.Accounts;

/// <summary>Body of <c>POST /api/account/password</c>; other devices are signed out afterwards.</summary>
public sealed record ChangePasswordDto(string CurrentPassword, string NewPassword);
