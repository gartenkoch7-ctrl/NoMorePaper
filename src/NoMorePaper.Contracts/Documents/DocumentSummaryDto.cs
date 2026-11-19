using NoMorePaper.Contracts.Classification;

namespace NoMorePaper.Contracts.Documents;

/// <summary>One row of the document list.</summary>
/// <param name="IsPrivate">Only its owner sees it.</param>
/// <param name="IsMine">The signed-in account owns it (and may change <paramref name="IsPrivate"/>).</param>
/// <param name="Correspondent">Who sent it; <c>null</c> when not set.</param>
/// <param name="DocumentType">What it is; <c>null</c> when not set.</param>
/// <param name="DocumentDate">Date printed on the document; <c>null</c> when not set.</param>
/// <param name="Amount">Amount with currency; <c>null</c> when not set.</param>
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
