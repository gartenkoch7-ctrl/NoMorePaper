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
    /// Removes every stored file of a deleted document, in the inbox and in the archive; does nothing
    /// when there is none. Only for documents that are no longer recorded.
    /// </summary>
    /// <exception cref="IOException">A folder could not be removed.</exception>
    /// <exception cref="UnauthorizedAccessException">A folder could not be removed.</exception>
    void Delete(Guid id);

    /// <summary>Opens a stored file for reading; the original is found in the archive or, before processing, in the inbox.</summary>
    /// <exception cref="FileNotFoundException">No such file is stored.</exception>
    Stream OpenRead(Guid id, DocumentFileKind kind);
}
