namespace NoMorePaper.Application.Classification;

/// <summary>A document in "to review": recognized automatically or a possible duplicate, editable by the account.</summary>
/// <param name="PossibleDuplicate">The document it may duplicate, if the account may see it.</param>
public sealed record ReviewItem(
    Guid Id,
    string Title,
    DateTimeOffset ReceivedAt,
    DocumentFieldsView Fields,
    DuplicateCandidate? PossibleDuplicate);
