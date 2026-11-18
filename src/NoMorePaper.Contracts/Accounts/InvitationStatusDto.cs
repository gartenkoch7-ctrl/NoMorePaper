namespace NoMorePaper.Contracts.Accounts;

/// <summary>A valid invitation and when it expires.</summary>
public sealed record InvitationStatusDto(DateTimeOffset ExpiresAt);
