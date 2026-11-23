using NoMorePaper.Domain.Editing;

namespace NoMorePaper.Application.Classification;

/// <summary>
/// A document in "to review": recognized automatically, a possible duplicate, waiting for its owner's check,
/// or a scanned stack with a split suggestion; editable by the account.
/// </summary>
/// <param name="PossibleDuplicate">The document it may duplicate, if the account may see it.</param>
/// <param name="Split">Where the scanned stack probably splits; <c>null</c> without a suggestion.</param>
public sealed record ReviewItem(
    Guid Id,
    string Title,
    DateTimeOffset ReceivedAt,
    DocumentFieldsView Fields,
    DuplicateCandidate? PossibleDuplicate,
    SplitSuggestion? Split = null);
