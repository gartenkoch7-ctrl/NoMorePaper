using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Documents;

/// <summary>A single document with its files; <paramref name="Archive"/> is <c>null</c> until processing is done.</summary>
/// <param name="Original">The file as received; <c>null</c> when the owner keeps only the edited version.</param>
/// <param name="IsMine">Whether the account that asked owns the document.</param>
/// <param name="Edited">The newest edited version, or <c>null</c>.</param>
/// <param name="EditedAt">When <paramref name="Edited"/> was made.</param>
/// <param name="EditPending">An edit is planned or running; the files shown are the previous version until it is done.</param>
/// <param name="VersionPolicy">Which versions the document keeps: its own choice, else the owner's default.</param>
/// <param name="FilesMoving">
/// A new version is stored, but its files are still moving to their place (<see cref="Editing.EditData.IsCommitted"/>):
/// the files named here may not be those on disk yet.
/// </param>
public sealed record DocumentDetail(
    Guid Id,
    string Title,
    DateTimeOffset ReceivedAt,
    ProcessingState State,
    string? FailureReason,
    DocumentFile? Original,
    DocumentFile? Archive,
    bool IsPrivate,
    bool IsMine,
    DocumentFile? Edited = null,
    DateTimeOffset? EditedAt = null,
    bool EditPending = false,
    VersionPolicy VersionPolicy = VersionPolicy.KeepBoth,
    bool FilesMoving = false);
