using NoMorePaper.Domain.Classification;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Classification;

/// <summary>
/// The fields of documents in the database: loading a document to change them, and the read side of
/// fields and "to review". Every query filters by what the viewer may see, so another account's private
/// document never leaves the database, not even as a duplicate hint.
/// </summary>
public interface IDocumentFieldsStore
{
    /// <summary>
    /// The document with its tags and editors, tracked for <see cref="SaveAsync"/>. It stays locked against
    /// the Worker finishing a recognition until <see cref="SaveAsync"/> or <see cref="DeleteAsync"/>.
    /// </summary>
    /// <returns><c>null</c> if it does not exist or <paramref name="viewerId"/> may not see it.</returns>
    Task<Document?> FindVisibleAsync(Guid documentId, Guid viewerId, CancellationToken cancellationToken);

    /// <summary>Whether <paramref name="viewerId"/> may see every one of these entries (visibility rule of the lists).</summary>
    Task<bool> AreVisibleAsync(Guid viewerId, ClassificationKind kind, IReadOnlyCollection<Guid> entryIds, CancellationToken cancellationToken);

    /// <summary>
    /// Whether <paramref name="audience"/> may see every one of these entries: the account's view, or only what
    /// the whole household sees already (<see cref="EntryAudience"/>, as recognition uses it). Read inside the
    /// open change, so a document loaded by <see cref="FindVisibleAsync"/> counts as it is stored, not as changed.
    /// </summary>
    Task<bool> AreVisibleAsync(
        Guid viewerId, ClassificationKind kind, IReadOnlyCollection<Guid> entryIds, EntryAudience audience, CancellationToken cancellationToken);

    /// <summary>Writes the changes of the documents loaded by <see cref="FindVisibleAsync"/>; only changed columns.</summary>
    Task SaveAsync(CancellationToken cancellationToken);

    /// <summary>Deletes a document loaded by <see cref="FindVisibleAsync"/> with its file records, tags, editors and job.</summary>
    Task DeleteAsync(Document document, CancellationToken cancellationToken);

    /// <returns>
    /// The fields as <paramref name="viewerId"/> sees them, or <c>null</c> if the document does not exist
    /// or the viewer may not see it. The duplicate hint is left out when the viewer may not see the other document.
    /// </returns>
    Task<DocumentFieldsView?> GetFieldsAsync(Guid documentId, Guid viewerId, CancellationToken cancellationToken);

    /// <summary>
    /// The documents <paramref name="viewerId"/> may edit that were recognized automatically or are a
    /// possible duplicate of a document the viewer may see, and the viewer's own documents waiting for
    /// the owner check (<see cref="Document.NeedsOwnerCheck"/>), newest first.
    /// </summary>
    Task<IReadOnlyList<ReviewItem>> ListReviewAsync(Guid viewerId, CancellationToken cancellationToken);

    /// <summary>How many documents <see cref="ListReviewAsync"/> returns for <paramref name="viewerId"/>, by the same rule.</summary>
    Task<int> CountReviewAsync(Guid viewerId, CancellationToken cancellationToken);
}
