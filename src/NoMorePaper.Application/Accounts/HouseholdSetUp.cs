namespace NoMorePaper.Application.Accounts;

/// <summary>The administrator created at setup, and how many documents without owner (from M1) it received.</summary>
public sealed record HouseholdSetUp(Account Administrator, int AdoptedDocuments);
