using NoMorePaper.Application.Documents;
using NoMorePaper.Contracts.Documents;

namespace NoMorePaper.Server.Documents;

/// <summary>
/// <c>GET /api/documents/{id}/text</c>, signed in only (group <c>/api</c>). A private document of another
/// account answers 404, like one that does not exist.
/// </summary>
internal static class DocumentTextEndpoints
{
    public static RouteGroupBuilder MapDocumentTextEndpoints(this RouteGroupBuilder api)
    {
        api.MapGroup("/documents/{id:guid}").MapGet("/text", GetAsync);
        return api;
    }

    private static async Task<IResult> GetAsync(
        Guid id, GetDocumentText get, ILoggerFactory loggers, CancellationToken cancellationToken)
    {
        var text = await get.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (text is null)
        {
            return Results.Problem(title: "Not found.", statusCode: StatusCodes.Status404NotFound);
        }

        // A static class cannot be a logger category type, so the category is its name, like the other endpoints.
        loggers.CreateLogger("NoMorePaper.Server.Documents.DocumentTextEndpoints").DocumentTextServed(id, text.Text?.Length ?? 0);
        return Results.Ok(new DocumentTextDto(text.Text));
    }
}
