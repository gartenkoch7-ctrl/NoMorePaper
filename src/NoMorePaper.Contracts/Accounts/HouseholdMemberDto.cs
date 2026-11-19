namespace NoMorePaper.Contracts.Accounts;

/// <summary>A member of the household as every signed-in account may see it (<c>GET /api/household</c>), e.g. to choose editors.</summary>
/// <param name="DisplayName">Name shown to the household.</param>
public sealed record HouseholdMemberDto(Guid Id, string DisplayName);
