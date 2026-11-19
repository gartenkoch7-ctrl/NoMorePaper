using Microsoft.AspNetCore.Http.Features;
using NoMorePaper.Application.Documents;
using NoMorePaper.Contracts.Documents;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Server.Accounts;

namespace NoMorePaper.Server.Documents;

/// <summary>
/// <c>POST /api/documents/pages</c>: the pages the camera took (JPEG, cropped and straightened in the
/// browser) become one PDF, one page per image, which is received like an upload with source
/// <see cref="DocumentSource.Camera"/>. Form: parts <c>page</c> in order, <c>private</c>, <c>takenAt</c>
/// (see <see cref="PageUploadForm"/>, <see cref="PageUploadName"/>). Answers like the upload: 202 with
/// <see cref="UploadResultDto"/>, 400, 413, 401 for a removed account, 409 with <c>existingId</c>.
/// </summary>
internal sealed class PageUpload(
    ReceiveDocument receive,
    IPdfComposer composer,
    AccountSessions sessions,
    IConfiguration configuration,
    TimeProvider clock,
    ILogger<PageUpload> logger)
{
    // Same key as InfrastructureSettings.DataPathKey; the Server may name Infrastructure types only in
    // its composition root, so the key is repeated here.
    private const string DataPathKey = "NoMorePaper:DataPath";
    private const string DefaultDataPath = "/data";

    /// <summary>Folder below the data path for the PDF while it is received; on the data volume, not in the container.</summary>
    public const string TempFolderName = "tmp";

    public async Task<IResult> HandleAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ContentLength > PageUploadForm.MaxRequestBytes)
        {
            return Reject(new("too-large", StatusCodes.Status413PayloadTooLarge, "The pages are too large."));
        }

        // Kestrel's default (30 MB) would cut off valid requests; chunked bodies are limited here too.
        if (request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
        {
            limit.MaxRequestBodySize = PageUploadForm.MaxRequestBytes;
        }

        if (!request.HasFormContentType)
        {
            return Reject(new("not-multipart", StatusCodes.Status400BadRequest, "Send the pages as multipart form parts 'page'."));
        }

        IFormCollection form;
        try
        {
            form = await request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (BadHttpRequestException exception) when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return Reject(new("too-large", StatusCodes.Status413PayloadTooLarge, "The pages are too large."));
        }
        catch (Exception exception) when (exception is InvalidDataException or BadHttpRequestException)
        {
            return Reject(new("malformed", StatusCodes.Status400BadRequest, "The pages could not be read."));
        }

        var (pages, refusal) = await PageUploadForm.CheckAsync(form, cancellationToken).ConfigureAwait(false);
        return pages is null
            ? Reject(refusal!)
            : await ReceiveAsync(request.HttpContext, pages, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IResult> ReceiveAsync(HttpContext context, PageUploadForm pages, CancellationToken cancellationToken)
    {
        // The PDF may be as large as all pages together: it goes to a file, not into memory a second
        // time. DeleteOnClose removes it whatever happens below.
        var folder = Path.Combine(DataPath(), TempFolderName);
        Directory.CreateDirectory(folder);
        var pdf = new FileStream(Path.Combine(folder, $"{Guid.NewGuid():N}.pdf"), new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.ReadWrite,
            Share = FileShare.None,
            Options = FileOptions.Asynchronous | FileOptions.DeleteOnClose,
        });
        await using (pdf.ConfigureAwait(false))
        {
            if (!await ComposeAsync(pages, pdf, cancellationToken).ConfigureAwait(false))
            {
                return Results.Problem(title: "The pages could not be read as images.", statusCode: StatusCodes.Status400BadRequest);
            }

            pdf.Position = 0;
            var name = PageUploadName.For(pages.TakenAt, clock.GetLocalNow());
            var received = await receive.ReceiveAsync(name, "application/pdf", pdf, pages.IsPrivate, DocumentSource.Camera, cancellationToken)
                .ConfigureAwait(false);
            switch (received)
            {
                case { Status: ReceiveStatus.Received, DocumentId: { } id, IsPrivate: var isPrivateApplied }:
                    logger.PagesAccepted(id, pages.Pages.Count, pages.SizeBytes);
                    return Results.Accepted($"/api/documents/{id}", new UploadResultDto(id, isPrivateApplied));

                case { Status: ReceiveStatus.Duplicate, DocumentId: { } existingId }:
                    return Reject(
                        new("duplicate", StatusCodes.Status409Conflict, "These pages are archived already."),
                        new Dictionary<string, object?> { [DocumentUpload.ExistingIdExtension] = existingId });

                case { Status: ReceiveStatus.AccountGone }:
                    return await sessions.EndRemovedAsync(context).ConfigureAwait(false);

                default:
                    throw new InvalidOperationException($"Unexpected intake result {received.Status}.");
            }
        }
    }

    /// <returns>False if an image could not be read (logged; nothing usable was written).</returns>
    private async Task<bool> ComposeAsync(PageUploadForm pages, Stream target, CancellationToken cancellationToken)
    {
        var streams = new List<Stream>(pages.Pages.Count);
        try
        {
            streams.AddRange(pages.Pages.Select(page => page.OpenReadStream()));
            await composer.ComposeImagesAsync(streams, target, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (InvalidDataException exception)
        {
            logger.PagesPdfFailed(pages.Pages.Count, exception.InnerException?.GetType().Name ?? exception.GetType().Name);
            return false;
        }
        finally
        {
            foreach (var stream in streams)
            {
                await stream.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private string DataPath() =>
        configuration[DataPathKey] is { } path && !string.IsNullOrWhiteSpace(path) ? path : DefaultDataPath;

    private IResult Reject(PageUploadRefusal refusal, IDictionary<string, object?>? extensions = null)
    {
        logger.PagesRejected(refusal.Reason);
        return Results.Problem(title: refusal.Title, statusCode: refusal.Status, extensions: extensions);
    }
}
