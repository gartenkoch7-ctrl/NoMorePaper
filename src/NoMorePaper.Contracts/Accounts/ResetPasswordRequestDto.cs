namespace NoMorePaper.Contracts.Accounts;

/// <summary>Sets a new password using a reset link.</summary>
public sealed record ResetPasswordRequestDto(string NewPassword);
