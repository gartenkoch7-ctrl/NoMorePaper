namespace NoMorePaper.Contracts.Accounts;

/// <summary>A one-time link (invitation or password reset) and when it stops working.</summary>
public sealed record InvitationLinkDto(string Link, DateTimeOffset ExpiresAt);
