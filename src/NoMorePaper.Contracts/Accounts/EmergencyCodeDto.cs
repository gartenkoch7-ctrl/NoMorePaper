namespace NoMorePaper.Contracts.Accounts;

/// <summary>
/// The administrator's emergency code (20 Base32 characters), shown exactly once: after setup and
/// after each emergency reset, which replaces it. Only its hash is stored.
/// </summary>
public sealed record EmergencyCodeDto(string EmergencyCode);
