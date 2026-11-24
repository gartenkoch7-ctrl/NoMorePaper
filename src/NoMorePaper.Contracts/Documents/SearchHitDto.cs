using NoMorePaper.Contracts.Classification;

namespace NoMorePaper.Contracts.Documents;

/// <summary>One search result, best first.</summary>
/// <param name="Snippet">Plain-text excerpt around the match; clients must never render it as HTML.</param>
/// <param name="Rank">Relevance; higher is better, only comparable within one search.</param>
/// <param name="IsPrivate">Only its owner sees it.</param>
/// <param name="IsMine">The signed-in account owns it.</param>
/// <param name="Correspondent">Who sent it; <c>null</c> when not set.</param>
/// <param name="DocumentType">What it is; <c>null</c> when not set.</param>
/// <param name="DocumentDate">Date printed on the document; <c>null</c> when not set.</param>
/// <param name="Amount">Amount with currency; <c>null</c> when not set.</param>
/// <param name="Recognition">Where the fields come from.</param>
/// <param name="EditedAt">When its newest edited version was made; <c>null</c> without one. Part of the thumbnail address, so an edit shows.</param>
public sealed record SearchHitDto(
    Guid Id,
    string Title,
    DateTimeOffset ReceivedAt,
    string Snippet,
    double Rank,
    bool IsPrivate = false,
    bool IsMine = false,
    EntryDto? Correspondent = null,
    EntryDto? DocumentType = null,
    DateOnly? DocumentDate = null,
    MoneyDto? Amount = null,
    DocumentRecognitionState Recognition = DocumentRecognitionState.None,
    DateTimeOffset? EditedAt = null);
