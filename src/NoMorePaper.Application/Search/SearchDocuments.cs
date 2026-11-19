using NoMorePaper.Application.Accounts;
using NoMorePaper.Application.Documents;

namespace NoMorePaper.Application.Search;

/// <summary>
/// Searches the finished documents the signed-in account may see; the query is plain text, never SQL
/// or query syntax.
/// </summary>
public sealed class SearchDocuments(ISearchIndex index, ICurrentAccount current)
{
    public const int MaxLimit = 50;

    /// <summary>Longer queries are cut: they only cost time, nobody searches with a paragraph.</summary>
    public const int MaxQueryLength = 200;

    /// <param name="limit">Clamped to 1–50.</param>
    /// <param name="filter">Narrows the hits like the document list.</param>
    /// <returns>Best hits first; empty for an empty query.</returns>
    public Task<IReadOnlyList<SearchHit>> SearchAsync(string? query, int limit, DocumentFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var text = Normalize(query);
        return text.Length == 0
            ? Task.FromResult<IReadOnlyList<SearchHit>>([])
            : index.SearchAsync(text, current.Id, Math.Clamp(limit, 1, MaxLimit), filter, cancellationToken);
    }

    // NUL cannot be stored in Postgres text and would fail the whole request.
    private static string Normalize(string? query)
    {
        var text = string.Concat((query ?? string.Empty).Where(c => c != '\0')).Trim();
        return text.Length > MaxQueryLength ? text[..MaxQueryLength] : text;
    }
}
