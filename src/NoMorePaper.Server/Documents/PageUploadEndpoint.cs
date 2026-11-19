namespace NoMorePaper.Server.Documents;

/// <summary>Route of the camera page upload, inside the signed-in <c>/api/documents</c> group.</summary>
internal static class PageUploadEndpoint
{
    /// <summary>Maps <c>POST /pages</c>; <see cref="PageUpload"/> is built per request from the request's services.</summary>
    public static RouteHandlerBuilder MapPageUpload(this RouteGroupBuilder documents) =>
        documents.MapPost("/pages", (HttpContext context, CancellationToken cancellationToken) =>
            ActivatorUtilities.CreateInstance<PageUpload>(context.RequestServices).HandleAsync(context.Request, cancellationToken));
}
