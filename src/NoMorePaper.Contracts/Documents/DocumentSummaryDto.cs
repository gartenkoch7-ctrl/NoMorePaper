using NoMorePaper.Contracts.Classification;

namespace NoMorePaper.Contracts.Documents;

/// <summary>One row of the document list.</summary>
/// <param name="IsPrivate">Only its owner sees it.</param>
/// <param name="IsMine">The signed-in account owns it (and may change <paramref name="IsPrivate"/>).</param>
/// <param name="Correspondent">Who sent it; <c>null</c> when unknown or not visible to the signed-in account.</param>
/// <param name="DocumentType">Kind of document, e.g. an invoice.</param>
/// <param name="DocumentDate">Date printed on the document.</param>
/// <param name="Amount">Amount with currency.</param>
/// <param name="Recognition">Where the fields come from.</param>
public sealed record DocumentSummaryDto(
    Guid Id,
    string Title,
    DateTimeOffset ReceivedAt,
    DocumentProcessingState State,
    bool IsPrivate = false,
    bool IsMine = false,
    EntryDto? Correspondent = null,
    EntryDto? DocumentType = null,
    DateOnly? DocumentDate = null,
    MoneyDto? Amount = null,
    DocumentRecognitionState Recognition = DocumentRecognitionState.None);
