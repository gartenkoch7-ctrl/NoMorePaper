namespace NoMorePaper.Contracts.Classification;

/// <summary>The document a review item may duplicate, with what is needed to compare both.</summary>
/// <param name="DocumentDate">Date printed on the document.</param>
public sealed record DuplicateCandidateDto(
    Guid Id,
    string Title,
    DateTimeOffset ReceivedAt,
    DateOnly? DocumentDate,
    EntryDto? Correspondent);
