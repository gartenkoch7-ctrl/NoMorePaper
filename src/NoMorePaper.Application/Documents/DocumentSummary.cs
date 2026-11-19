using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Documents;

/// <summary>One row of the document list; the Server maps it to the Contracts DTO (Infrastructure must not know Contracts).</summary>
/// <param name="IsMine">Whether the account that asked owns the document.</param>
/// <param name="Labels">Correspondent, type, date, amount and recognition state.</param>
public sealed record DocumentSummary(
    Guid Id, string Title, DateTimeOffset ReceivedAt, ProcessingState State, bool IsPrivate, bool IsMine, DocumentLabels Labels);
