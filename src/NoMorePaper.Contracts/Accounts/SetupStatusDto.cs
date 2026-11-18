namespace NoMorePaper.Contracts.Accounts;

/// <summary>Whether the first-run setup is still needed (no account exists yet).</summary>
public sealed record SetupStatusDto(bool Needed);
