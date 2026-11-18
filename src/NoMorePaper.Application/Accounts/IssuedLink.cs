namespace NoMorePaper.Application.Accounts;

/// <summary>A one-time link token for the caller to hand out; only its hash is stored.</summary>
public sealed record IssuedLink(string Token, DateTimeOffset ExpiresAt);
