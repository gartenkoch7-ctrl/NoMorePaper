namespace NoMorePaper.Application.Search;

/// <summary>Full-text index over finished documents (state <c>Done</c>); visibility is filtered in the query.</summary>
public interface ISearchIndex
{
    /// <param name="text">What to look for; not empty, at most <see cref="SearchDocuments.MaxQueryLength"/> characters, no NUL.</param>
    /// <param name="viewerId">Only documents this account may see: its own and all that are not private.</param>
    /// <param name="limit">Most hits returned, 1 to <see cref="SearchDocuments.MaxLimit"/>.</param>
    /// <returns>Best hits first.</returns>
    Task<IReadOnlyList<SearchHit>> SearchAsync(string text, Guid viewerId, int limit, CancellationToken cancellationToken);
}
