namespace NoMorePaper.Contracts.Accounts;

/// <summary>Answer of <c>GET /api/setup</c>: <paramref name="Needed"/> while no account exists.</summary>
public sealed record SetupStatusDto(bool Needed);
