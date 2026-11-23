using NoMorePaper.Contracts.Editing;

namespace NoMorePaper.Contracts.Classification;

/// <summary>
/// A document in the list "to review": recognized automatically or a possible duplicate, and editable
/// by the signed-in account.
/// </summary>
/// <param name="PossibleDuplicate">The document it may duplicate; <c>null</c> when there is none.</param>
/// <param name="Split">Where the scanned stack probably splits; <c>null</c> without a suggestion.</param>
public sealed record ReviewItemDto(
    Guid Id,
    string Title,
    DateTimeOffset ReceivedAt,
    DocumentFieldsDto Fields,
    DuplicateCandidateDto? PossibleDuplicate,
    SplitSuggestionDto? Split = null);
