namespace NoMorePaper.Application.Search;

/// <summary>Full-text index over finished documents (state <c>Done</c>).</summary>
public interface ISearchIndex
{
    /// <param name="text">What to look for; not empty, at most <see cref="SearchDocuments.MaxQueryLength"/> characters, no NUL.</param>
    /// <param name="limit">Most hits returned, 1 to <see cref="SearchDocuments.MaxLimit"/>.</param>
    /// <returns>Best hits first.</returns>
    Task<IReadOnlyList<SearchHit>> SearchAsync(string text, int limit, CancellationToken cancellationToken);
}
