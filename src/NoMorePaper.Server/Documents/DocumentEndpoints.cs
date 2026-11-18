using NoMorePaper.Application.Documents;
using NoMorePaper.Contracts.Documents;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Documents;

namespace NoMorePaper.Server.Documents;

/// <summary>Routes under <c>/api/documents</c>: upload, list, detail and file download.</summary>
internal static class DocumentEndpoints
{
    private const int DefaultPageSize = 50;

    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/documents");

        // No antiforgery: the form is bound by hand, the API has no cookies or sessions to ride on, and
        // CrossSiteRequestGuard refuses posts from other sites.
        group.MapPost("/", (HttpRequest request, DocumentUpload upload, CancellationToken cancellationToken) =>
            upload.HandleAsync(request, cancellationToken));
        group.MapGet("/", ListAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapGet("/{id:guid}/files/{kind}", DownloadAsync);
        return routes;
    }

    private static async Task<IResult> ListAsync(
        DocumentQueries queries, CancellationToken cancellationToken, int page = 1, int pageSize = DefaultPageSize)
    {
        var documents = await queries.ListAsync(page, pageSize, cancellationToken).ConfigureAwait(false);
        return Results.Ok(documents.Select(document => document.ToDto()));
    }

    private static async Task<IResult> GetAsync(Guid id, DocumentQueries queries, CancellationToken cancellationToken)
    {
        var detail = await queries.GetAsync(id, cancellationToken).ConfigureAwait(false);
        return detail is null ? NotFound() : Results.Ok(detail.ToDto());
    }

    // The bytes are served as stored: same SHA-256 as at upload, never converted (ADR-0003).
    private static async Task<IResult> DownloadAsync(
        Guid id, string kind, HttpContext context, DocumentQueries queries, IDocumentStorage storage, CancellationToken cancellationToken)
    {
        var fileKind = kind.ToLowerInvariant() switch
        {
            "original" => DocumentFileKind.Original,
            "archive" => DocumentFileKind.Archive,
            _ => (DocumentFileKind?)null,
        };
        if (fileKind is null)
        {
            return NotFound();
        }

        var detail = await queries.GetAsync(id, cancellationToken).ConfigureAwait(false);
        var file = fileKind == DocumentFileKind.Original ? detail?.Original : detail?.Archive;
        if (file is null)
        {
            return NotFound();
        }

        Stream stream;
        try
        {
            stream = storage.OpenRead(id, fileKind.Value);
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }

        // The type was detected from the content at upload; nosniff keeps browsers from second-guessing it.
        context.Response.Headers.XContentTypeOptions = "nosniff";
        return Results.File(stream, file.MediaType, file.FileName, enableRangeProcessing: true);
    }

    private static IResult NotFound() => Results.Problem(title: "Not found.", statusCode: StatusCodes.Status404NotFound);
}
