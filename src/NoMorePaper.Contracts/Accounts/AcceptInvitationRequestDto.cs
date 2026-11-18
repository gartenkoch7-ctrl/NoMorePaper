namespace NoMorePaper.Contracts.Accounts;

/// <summary>Creates a member account from an invitation.</summary>
public sealed record AcceptInvitationRequestDto(string UserName, string DisplayName, string Password);
