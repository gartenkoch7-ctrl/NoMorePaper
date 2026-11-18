using NoMorePaper.Infrastructure.Search;
using NoMorePaper.Server.Documents;

namespace NoMorePaper.Server.Search;

/// <summary><c>GET /api/search?q=&amp;limit=20</c>.</summary>
internal static class SearchEndpoints
{
    private const int DefaultLimit = 20;

    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/search", SearchAsync);
        return routes;
    }

    // An empty query is not an error: the search box is empty before the user typed anything.
    private static async Task<IResult> SearchAsync(
        DocumentSearch search, CancellationToken cancellationToken, string? q = null, int limit = DefaultLimit)
    {
        var hits = await search.SearchAsync(q ?? string.Empty, Math.Clamp(limit, 1, DocumentSearch.MaxLimit), cancellationToken)
            .ConfigureAwait(false);
        return Results.Ok(hits.Select(hit => hit.ToDto()));
    }
}
