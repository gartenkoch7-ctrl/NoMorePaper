using NoMorePaper.Application.Classification;

namespace NoMorePaper.Application.Recognition;

/// <summary>Confirmed documents as examples for recognition, read from the database.</summary>
public interface IRecognitionExamples
{
    /// <summary>
    /// Up to <paramref name="limit"/> confirmed documents of the given correspondents that
    /// <paramref name="audience"/> may see: those of the first correspondent first, newest first within one.
    /// Documents the audience may not see are filtered in the query, so they never become examples.
    /// </summary>
    /// <param name="ownerId">Owner of the document being recognized.</param>
    /// <param name="audience">
    /// <see cref="EntryAudience.Account"/>: the owner's documents and all that are not private;
    /// <see cref="EntryAudience.Household"/>: only those that are not private.
    /// </param>
    /// <param name="correspondentIds">Correspondents in the order they are mentioned in the text.</param>
    /// <param name="exceptDocumentId">The document being recognized, never its own example.</param>
    Task<IReadOnlyList<RecognitionExample>> FindAsync(
        Guid ownerId,
        EntryAudience audience,
        IReadOnlyList<Guid> correspondentIds,
        Guid exceptDocumentId,
        int limit,
        CancellationToken cancellationToken);
}
