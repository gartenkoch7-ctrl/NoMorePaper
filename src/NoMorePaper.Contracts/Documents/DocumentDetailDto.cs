using NoMorePaper.Contracts.Editing;

namespace NoMorePaper.Contracts.Documents;

/// <summary>A single document with its files.</summary>
/// <param name="FailureReason">Technical reason when <paramref name="State"/> is <c>Failed</c>, otherwise <c>null</c>.</param>
/// <param name="Original">The file as received; <c>null</c> when the owner keeps only the edited version.</param>
/// <param name="Archive">The PDF/A; <c>null</c> until processing is done.</param>
/// <param name="IsPrivate">Only its owner sees it.</param>
/// <param name="IsMine">The signed-in account owns it (and may change <paramref name="IsPrivate"/>).</param>
/// <param name="Edited">The newest edited version (<c>/files/edited</c>); <c>null</c> without one.</param>
/// <param name="EditedAt">When <paramref name="Edited"/> was made.</param>
/// <param name="EditPending">An edit waits or runs; the files are the previous version until it is done.</param>
/// <param name="VersionPolicy">Which versions the document keeps: its own choice, else the owner's default.</param>
public sealed record DocumentDetailDto(
    Guid Id,
    string Title,
    DateTimeOffset ReceivedAt,
    DocumentProcessingState State,
    string? FailureReason,
    DocumentFileDto? Original,
    DocumentFileDto? Archive,
    bool IsPrivate = false,
    bool IsMine = false,
    DocumentFileDto? Edited = null,
    DateTimeOffset? EditedAt = null,
    bool EditPending = false,
    VersionPolicyDto VersionPolicy = VersionPolicyDto.KeepBoth);
