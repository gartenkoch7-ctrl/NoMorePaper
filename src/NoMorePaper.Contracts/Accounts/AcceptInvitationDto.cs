namespace NoMorePaper.Contracts.Accounts;

/// <summary>Body of <c>POST /api/invitations/{token}/accept</c>: the new member, signed in on success.</summary>
public sealed record AcceptInvitationDto(string UserName, string DisplayName, string Password);
