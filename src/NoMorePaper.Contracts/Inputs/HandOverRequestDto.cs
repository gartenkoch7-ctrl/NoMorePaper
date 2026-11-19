namespace NoMorePaper.Contracts.Inputs;

/// <summary>Hands a document whose owner was unknown to another member (<c>POST /api/documents/{id}/hand-over</c>).</summary>
public sealed record HandOverRequestDto(Guid AccountId);
