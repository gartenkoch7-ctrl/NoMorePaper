using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Documents;

/// <summary>
/// Keeps the bytes of document files (ADR-0003). A received original waits in the inbox until the
/// Worker has produced the archive file and moves the original next to it. Every write is atomic:
/// readers see the old file or the complete new one, never a partial file.
/// </summary>
public interface IDocumentStorage
{
    /// <summary>Stores a received original in the inbox, replacing an earlier one of the same document.</summary>
    /// <param name="id">Document id (version 7 GUID).</param>
    /// <param name="fileName">Name the file was received with; only its extension is used, never its path.</param>
    Task<StoredFile> SaveInboxAsync(Guid id, string fileName, Stream content, CancellationToken cancellationToken);

    /// <summary>Stores a file produced by processing in the archive, replacing an earlier one.</summary>
    /// <param name="kind">Only <see cref="DocumentFileKind.Archive"/>; originals reach the archive through <see cref="ArchiveOriginalAsync"/>.</param>
    Task<StoredFile> SaveArchiveAsync(Guid id, DocumentFileKind kind, Stream content, CancellationToken cancellationToken);

    /// <summary>
    /// Moves the original from the inbox into the archive without copying it. Does nothing when
    /// it was moved already, so a retried job can call it again.
    /// </summary>
    Task ArchiveOriginalAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Removes what the inbox holds for a document whose intake failed; does nothing when there is
    /// nothing. Only for originals that no recorded document refers to.
    /// </summary>
    /// <exception cref="IOException">The inbox folder could not be removed.</exception>
    /// <exception cref="UnauthorizedAccessException">The inbox folder could not be removed.</exception>
    void DiscardInbox(Guid id);

    /// <summary>
    /// Removes every stored file of a deleted document, in the inbox, the archive and pending edits; does
    /// nothing when there is none. Only for documents that are no longer recorded.
    /// </summary>
    /// <exception cref="IOException">A folder could not be removed.</exception>
    /// <exception cref="UnauthorizedAccessException">A folder could not be removed.</exception>
    void Delete(Guid id);

    /// <summary>Opens a stored file for reading; the original is found in the archive or, before processing, in the inbox.</summary>
    /// <exception cref="FileNotFoundException">No such file is stored.</exception>
    Stream OpenRead(Guid id, DocumentFileKind kind);

    /// <summary>Stores the newest edited version (<c>edited.pdf</c>) next to the archive PDF, replacing an earlier one.</summary>
    Task<StoredFile> SaveEditedAsync(Guid id, Stream content, CancellationToken cancellationToken);

    /// <summary>
    /// Moves the original or the edited version of a processed document into the trash folder of the
    /// entry <paramref name="trashItemId"/>, without copying. Does nothing when it was moved already.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is the archive.</exception>
    /// <exception cref="FileNotFoundException">The file is neither in the archive nor in the trash folder.</exception>
    Task MoveToTrashAsync(Guid id, DocumentFileKind kind, Guid trashItemId, CancellationToken cancellationToken);

    /// <summary>
    /// Moves a file back from the trash folder of <paramref name="trashItemId"/> to its place in the archive
    /// and removes the then empty trash folder. Does nothing when it was moved back already.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is the archive.</exception>
    /// <exception cref="IOException">Its place is taken; the caller moves the other file to the trash first.</exception>
    /// <exception cref="FileNotFoundException">The file is neither in the trash folder nor in the archive.</exception>
    Task RestoreFromTrashAsync(Guid trashItemId, Guid id, DocumentFileKind kind, CancellationToken cancellationToken);

    /// <summary>Removes the trash folder of an entry for good; does nothing when there is none.</summary>
    /// <exception cref="IOException">The folder could not be removed.</exception>
    /// <exception cref="UnauthorizedAccessException">The folder could not be removed.</exception>
    void PurgeTrash(Guid trashItemId);
}
