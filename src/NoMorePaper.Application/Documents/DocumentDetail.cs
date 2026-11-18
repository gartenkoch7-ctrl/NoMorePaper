using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Documents;

/// <summary>A single document with its files; <paramref name="Archive"/> is <c>null</c> until processing is done.</summary>
/// <param name="IsMine">Whether the account that asked owns the document.</param>
public sealed record DocumentDetail(
    Guid Id,
    string Title,
    DateTimeOffset ReceivedAt,
    ProcessingState State,
    string? FailureReason,
    DocumentFile Original,
    DocumentFile? Archive,
    bool IsPrivate,
    bool IsMine);
