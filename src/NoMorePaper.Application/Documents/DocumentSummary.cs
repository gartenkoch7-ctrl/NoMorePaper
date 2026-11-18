using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Documents;

/// <summary>One row of the document list; the Server maps it to the Contracts DTO (Infrastructure must not know Contracts).</summary>
public sealed record DocumentSummary(Guid Id, string Title, DateTimeOffset ReceivedAt, ProcessingState State);
