namespace NoMorePaper.Contracts.Accounts;

/// <summary>Body of <c>POST /api/emergency-reset</c>; case, spaces and dashes in the code do not matter.</summary>
public sealed record EmergencyResetDto(string UserName, string EmergencyCode, string NewPassword);
