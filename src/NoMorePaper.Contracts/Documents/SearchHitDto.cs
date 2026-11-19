using NoMorePaper.Contracts.Classification;

namespace NoMorePaper.Contracts.Documents;

/// <summary>One search result, best first.</summary>
/// <param name="Snippet">Plain-text excerpt around the match; clients must never render it as HTML.</param>
/// <param name="Rank">Relevance; higher is better, only comparable within one search.</param>
/// <param name="IsPrivate">Only its owner sees it.</param>
/// <param name="IsMine">The signed-in account owns it.</param>
/// <param name="Correspondent">Who sent it; <c>null</c> when unknown or not visible to the signed-in account.</param>
/// <param name="DocumentType">Kind of document, e.g. an invoice.</param>
/// <param name="DocumentDate">Date printed on the document.</param>
/// <param name="Amount">Amount with currency.</param>
/// <param name="Recognition">Where the fields come from.</param>
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
    DocumentRecognitionState Recognition = DocumentRecognitionState.None);
