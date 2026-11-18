namespace NoMorePaper.Contracts.Documents;

/// <summary>A single document with its files.</summary>
/// <param name="FailureReason">Technical reason when <paramref name="State"/> is <c>Failed</c>, otherwise <c>null</c>.</param>
/// <param name="Archive">The PDF/A; <c>null</c> until processing is done.</param>
/// <param name="IsPrivate">Only its owner sees it.</param>
/// <param name="IsMine">The signed-in account owns it (and may change <paramref name="IsPrivate"/>).</param>
public sealed record DocumentDetailDto(
    Guid Id,
    string Title,
    DateTimeOffset ReceivedAt,
    DocumentProcessingState State,
    string? FailureReason,
    DocumentFileDto Original,
    DocumentFileDto? Archive,
    bool IsPrivate = false,
    bool IsMine = false);
