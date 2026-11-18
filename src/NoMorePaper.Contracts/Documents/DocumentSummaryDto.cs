namespace NoMorePaper.Contracts.Documents;

/// <summary>One row of the document list.</summary>
public sealed record DocumentSummaryDto(Guid Id, string Title, DateTimeOffset ReceivedAt, DocumentProcessingState State);
