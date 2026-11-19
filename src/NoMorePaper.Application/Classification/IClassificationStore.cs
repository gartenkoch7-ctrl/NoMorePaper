using NoMorePaper.Domain.Classification;

namespace NoMorePaper.Application.Classification;

/// <summary>
/// The maintained lists (correspondents, document types, tags) in the database. An entry is visible
/// to an account if a document the account may see uses it, or if no document uses it and the account
/// created it; every method filters by this rule in the query, so private entries never leave the database.
/// </summary>
public interface IClassificationStore
{
    /// <summary>The entries of the list that <paramref name="audience"/> may see, ordered by name.</summary>
    /// <param name="viewerId">The account for <see cref="EntryAudience.Account"/>.</param>
    Task<IReadOnlyList<EntrySummary>> VisibleEntriesAsync(
        Guid viewerId, ClassificationKind kind, EntryAudience audience, CancellationToken cancellationToken);

    /// <summary>
    /// The entry <paramref name="audience"/> may see whose normalized name equals that of
    /// <paramref name="name"/> (<see cref="ClassificationName.Normalize"/>), or a new entry created by
    /// <paramref name="actorId"/>. With <see cref="EntryAudience.Household"/> an equal entry used only in
    /// private documents (or by none) is not reused: the new one has the same name, which the lists allow.
    /// </summary>
    /// <exception cref="ArgumentException">The name is invalid (<see cref="ClassificationName.Clean"/>).</exception>
    Task<EntrySummary> FindOrCreateAsync(
        Guid actorId, ClassificationKind kind, string name, EntryAudience audience, CancellationToken cancellationToken);

    /// <summary>Renames an entry, if <paramref name="actorId"/> may edit every document that uses it.</summary>
    /// <exception cref="ArgumentException">The name is invalid (<see cref="ClassificationName.Clean"/>).</exception>
    Task<EntryChange> RenameAsync(Guid actorId, ClassificationKind kind, Guid entryId, string name, CancellationToken cancellationToken);

    /// <summary>
    /// Moves every document from <paramref name="sourceIds"/> to <paramref name="targetId"/> and deletes the
    /// sources, if <paramref name="actorId"/> may edit every document that uses any of these entries.
    /// </summary>
    /// <exception cref="ArgumentException">There is no source besides the target.</exception>
    Task<EntryChange> MergeAsync(
        Guid actorId,
        ClassificationKind kind,
        Guid targetId,
        IReadOnlyCollection<Guid> sourceIds,
        CancellationToken cancellationToken);
}
