namespace NoMorePaper.Contracts.Editing;

/// <summary>The default of the signed-in account for edited documents (<c>GET/PUT /api/account/after-editing</c>).</summary>
public sealed record AfterEditingDto(VersionPolicyDto Policy);
