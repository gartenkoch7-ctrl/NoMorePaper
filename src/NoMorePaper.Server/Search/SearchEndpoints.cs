using NoMorePaper.Application.Search;
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
        SearchDocuments search, CancellationToken cancellationToken, string? q = null, int limit = DefaultLimit)
    {
        var hits = await search.SearchAsync(q, limit, cancellationToken).ConfigureAwait(false);
        return Results.Ok(hits.Select(hit => hit.ToDto()));
    }
}
