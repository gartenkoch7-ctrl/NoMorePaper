namespace NoMorePaper.Application.Accounts;

/// <param name="EmergencyCode">In clear text for the one time it is shown; only its hash is stored.</param>
public sealed record SetupCompleted(Account Administrator, string EmergencyCode);
