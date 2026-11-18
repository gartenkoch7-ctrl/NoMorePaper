namespace NoMorePaper.Contracts.Documents;

/// <summary>One row of the document list.</summary>
/// <param name="IsPrivate">Only its owner sees it.</param>
/// <param name="IsMine">The signed-in account owns it (and may change <paramref name="IsPrivate"/>).</param>
public sealed record DocumentSummaryDto(
    Guid Id, string Title, DateTimeOffset ReceivedAt, DocumentProcessingState State, bool IsPrivate = false, bool IsMine = false);
