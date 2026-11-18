namespace NoMorePaper.Contracts.Accounts;

/// <summary>Changes the password of the signed-in account.</summary>
public sealed record ChangePasswordRequestDto(string CurrentPassword, string NewPassword);
