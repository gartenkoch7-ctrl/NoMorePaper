using NoMorePaper.Application.Search;
using NoMorePaper.Server.Documents;

namespace NoMorePaper.Server.Search;

/// <summary><c>GET /api/search?q=&amp;limit=20</c> plus the filters of the document list (<see cref="DocumentFilterQuery"/>), signed in only.</summary>
internal static class SearchEndpoints
{
    private const int DefaultLimit = 20;

    public static RouteGroupBuilder MapSearchEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/search", SearchAsync);
        return api;
    }

    // An empty query is not an error: the search box is empty before the user typed anything.
    private static async Task<IResult> SearchAsync(
        SearchDocuments search, DocumentFilterQuery query, CancellationToken cancellationToken, string? q = null, int limit = DefaultLimit)
    {
        if (query.Filter is not { } filter)
        {
            return Results.Problem(title: query.Error, statusCode: StatusCodes.Status400BadRequest);
        }

        var hits = await search.SearchAsync(q, limit, filter, cancellationToken).ConfigureAwait(false);
        return Results.Ok(hits.Select(hit => hit.ToDto()));
    }
}
