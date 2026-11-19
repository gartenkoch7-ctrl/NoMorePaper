namespace NoMorePaper.Application.Recognition;

/// <summary>Finds documents with (nearly) the same text and records the hint (M3 plan, "Inhaltliche Duplikate").</summary>
public interface IDuplicateFinder
{
    /// <summary>
    /// The done document most similar to <paramref name="documentId"/> by trigram similarity of their
    /// texts, among those its owner may see (own ones and all that are not private), if at least
    /// <paramref name="threshold"/> similar; the oldest among equally similar ones.
    /// </summary>
    /// <param name="threshold">Least similarity, between 0 and 1.</param>
    Task<Guid?> FindSimilarAsync(Guid documentId, double threshold, CancellationToken cancellationToken);

    /// <summary>Notes on <paramref name="documentId"/> that <paramref name="duplicateOf"/> has (nearly) the same content.</summary>
    /// <returns><c>false</c> when the document does not exist (any more).</returns>
    Task<bool> MarkAsync(Guid documentId, Guid duplicateOf, CancellationToken cancellationToken);
}
