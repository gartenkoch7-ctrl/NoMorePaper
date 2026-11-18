namespace NoMorePaper.Contracts.Accounts;

/// <summary>Answer of <c>GET /api/invitations/{token}</c> for an open invitation.</summary>
public sealed record InvitationDto(DateTimeOffset ExpiresAt);
