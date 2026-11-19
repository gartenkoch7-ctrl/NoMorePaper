namespace NoMorePaper.Contracts.Accounts;

/// <summary>A member of the household as offered when choosing who may edit a document's fields.</summary>
public sealed record HouseholdMemberDto(Guid Id, string DisplayName);
