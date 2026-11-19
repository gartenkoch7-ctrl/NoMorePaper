namespace NoMorePaper.Application.Recognition;

/// <summary>Confirmed documents as examples for recognition, read from the database.</summary>
public interface IRecognitionExamples
{
    /// <summary>
    /// Up to <paramref name="limit"/> confirmed documents of the given correspondents that
    /// <paramref name="ownerId"/> may see: those of the first correspondent first, newest first within one.
    /// Documents the owner may not see are filtered in the query, so they never become examples.
    /// </summary>
    /// <param name="ownerId">Owner of the document being recognized.</param>
    /// <param name="correspondentIds">Correspondents in the order they are mentioned in the text.</param>
    /// <param name="exceptDocumentId">The document being recognized, never its own example.</param>
    Task<IReadOnlyList<RecognitionExample>> FindAsync(
        Guid ownerId,
        IReadOnlyList<Guid> correspondentIds,
        Guid exceptDocumentId,
        int limit,
        CancellationToken cancellationToken);
}
