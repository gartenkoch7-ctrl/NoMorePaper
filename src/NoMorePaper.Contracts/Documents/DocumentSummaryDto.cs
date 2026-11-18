namespace NoMorePaper.Contracts.Documents;

/// <summary>One row of the document list.</summary>
/// <param name="IsPrivate">Only the owner sees a private document.</param>
/// <param name="IsMine">The signed-in account owns the document.</param>
public sealed record DocumentSummaryDto(
    Guid Id,
    string Title,
    DateTimeOffset ReceivedAt,
    DocumentProcessingState State,
    bool IsPrivate = false,
    bool IsMine = true);
