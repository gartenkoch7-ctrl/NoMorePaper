namespace NoMorePaper.Infrastructure.Classification;

/// <summary>The columns of a possible original that "to review" shows next to its duplicate.</summary>
internal sealed record CandidateRow(Guid Id, string Title, DateTimeOffset ReceivedAt, DateOnly? DocumentDate, Guid? CorrespondentId);
