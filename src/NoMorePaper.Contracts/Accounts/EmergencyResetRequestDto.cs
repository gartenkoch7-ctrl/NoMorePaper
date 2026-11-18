namespace NoMorePaper.Contracts.Accounts;

/// <summary>Sets a new password using the emergency code.</summary>
public sealed record EmergencyResetRequestDto(string UserName, string EmergencyCode, string NewPassword);
