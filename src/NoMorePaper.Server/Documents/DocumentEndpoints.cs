using NoMorePaper.Application.Documents;
using NoMorePaper.Contracts.Documents;
using NoMorePaper.Domain.Documents;

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
        ListDocuments list, CancellationToken cancellationToken, int page = 1, int pageSize = DefaultPageSize)
    {
        var documents = await list.ListAsync(page, pageSize, cancellationToken).ConfigureAwait(false);
        return Results.Ok(documents.Select(document => document.ToDto()));
    }

    private static async Task<IResult> GetAsync(Guid id, GetDocument get, CancellationToken cancellationToken)
    {
        var detail = await get.GetAsync(id, cancellationToken).ConfigureAwait(false);
        return detail is null ? NotFound() : Results.Ok(detail.ToDto());
    }

    // The bytes are served as stored: same SHA-256 as at upload, never converted (ADR-0003).
    private static async Task<IResult> DownloadAsync(
        Guid id, string kind, HttpContext context, OpenDocumentFile open, CancellationToken cancellationToken)
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

        var opened = await open.OpenAsync(id, fileKind.Value, cancellationToken).ConfigureAwait(false);
        if (opened is null)
        {
            return NotFound();
        }

        // The type was detected from the content at upload; nosniff keeps browsers from second-guessing it.
        context.Response.Headers.XContentTypeOptions = "nosniff";
        return Results.File(opened.Content, opened.File.MediaType, opened.File.FileName, enableRangeProcessing: true);
    }

    private static IResult NotFound() => Results.Problem(title: "Not found.", statusCode: StatusCodes.Status404NotFound);
}
