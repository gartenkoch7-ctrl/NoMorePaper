namespace NoMorePaper.Contracts.Editing;

/// <summary>The owner's choice for one document (<c>PUT /api/documents/{id}/versions</c>).</summary>
public sealed record VersionChoiceDto(VersionPolicyDto Policy);
