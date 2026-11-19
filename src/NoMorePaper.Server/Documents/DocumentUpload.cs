using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Http.Features;
using NoMorePaper.Contracts.Documents;
using NoMorePaper.Application.Documents;
using NoMorePaper.Server.Accounts;
using NoMorePaper.Server.Telemetry;

namespace NoMorePaper.Server.Documents;

/// <summary>
/// <c>POST /api/documents</c>: checks size and real file type before anything is stored. The uploader
/// becomes the owner; form field <c>private</c> (<c>true</c>/<c>false</c>) hides it from everybody else; without the field the
/// account's setting for new documents decides.
/// Errors are ProblemDetails: 400 nothing usable sent, 413 too large, 415 type not allowed, 401 the
/// account was removed, 409 the uploader may already see a document with exactly this file (extension
/// <c>existingId</c>). Scoped, like the <see cref="ReceiveDocument"/> use case it hands the file to.
/// </summary>
internal sealed class DocumentUpload(ReceiveDocument receive, AccountSessions sessions, ILogger<DocumentUpload> logger)
{
    public const long MaxFileBytes = 100L * 1024 * 1024;

    /// <summary>Room for the multipart framing around the file.</summary>
    public const long MaxRequestBytes = MaxFileBytes + (1024 * 1024);

    public const string FormField = "file";

    /// <summary>Optional form field: <c>true</c> keeps the document visible to the uploader only.</summary>
    public const string PrivateField = "private";

    /// <summary>ProblemDetails extension of a 409: id of the document with the same file.</summary>
    public const string ExistingIdExtension = "existingId";

    private static readonly Counter<long> s_rejected = ServerTelemetry.Meter.CreateCounter<long>(
        "nomorepaper.upload.rejected", description: "Uploads refused, by reason");

    public async Task<IResult> HandleAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ContentLength > MaxRequestBytes)
        {
            return Reject("too-large", StatusCodes.Status413PayloadTooLarge, "The file is too large.", request.ContentLength ?? 0);
        }

        // Kestrel's default (30 MB) would cut off valid uploads; chunked bodies are limited here too.
        if (request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
        {
            limit.MaxRequestBodySize = MaxRequestBytes;
        }

        if (!request.HasFormContentType)
        {
            return Reject("not-multipart", StatusCodes.Status400BadRequest, "Send the file as multipart form field 'file'.", 0);
        }

        IFormCollection form;
        try
        {
            form = await request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (BadHttpRequestException exception) when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return Reject("too-large", StatusCodes.Status413PayloadTooLarge, "The file is too large.", 0);
        }
        catch (Exception exception) when (exception is InvalidDataException or BadHttpRequestException)
        {
            return Reject("malformed", StatusCodes.Status400BadRequest, "The upload could not be read.", 0);
        }

        // Anything but true or false is refused: a typo must not silently share a private document.
        if (!TryReadPrivate(form, out var isPrivate))
        {
            return Reject("malformed", StatusCodes.Status400BadRequest, "Field 'private' must be true or false.", 0);
        }

        if (form.Files.GetFile(FormField) is not { Length: > 0 } file)
        {
            return Reject("empty", StatusCodes.Status400BadRequest, "No file was sent, or it is empty.", 0);
        }

        if (file.Length > MaxFileBytes)
        {
            return Reject("too-large", StatusCodes.Status413PayloadTooLarge, "The file is too large.", file.Length);
        }

        var mediaType = await DetectAsync(file, cancellationToken).ConfigureAwait(false);
        if (mediaType is null)
        {
            return Reject(
                "type-not-allowed", StatusCodes.Status415UnsupportedMediaType,
                "Only PDF, JPEG, PNG and HEIC files are accepted.", file.Length);
        }

        var content = file.OpenReadStream();
        await using (content.ConfigureAwait(false))
        {
            var received = await receive.ReceiveAsync(file.FileName, mediaType, content, isPrivate, cancellationToken).ConfigureAwait(false);
            switch (received)
            {
                case { Status: ReceiveStatus.Received, DocumentId: { } id, IsPrivate: var isPrivateApplied }:
                    logger.UploadAccepted(id, file.Length, mediaType);
                    return Results.Accepted($"/api/documents/{id}", new UploadResultDto(id, isPrivateApplied));

                case { Status: ReceiveStatus.Duplicate, DocumentId: { } existingId }:
                    return Reject(
                        "duplicate", StatusCodes.Status409Conflict, "This file is archived already.", file.Length,
                        new Dictionary<string, object?> { [ExistingIdExtension] = existingId });

                case { Status: ReceiveStatus.AccountGone }:
                    return await sessions.EndRemovedAsync(request.HttpContext).ConfigureAwait(false);

                default:
                    throw new InvalidOperationException($"Unexpected intake result {received.Status}.");
            }
        }
    }

    // No field: no choice (null), the account's setting for new documents decides.
    private static bool TryReadPrivate(IFormCollection form, out bool? isPrivate)
    {
        var values = form[PrivateField];
        isPrivate = null;
        if (values.Count == 0)
        {
            return true;
        }

        if (values.Count != 1 || !bool.TryParse(values[0], out var chosen))
        {
            return false;
        }

        isPrivate = chosen;
        return true;
    }

    private static async Task<string?> DetectAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var head = new byte[FileSignature.HeadLength];
        var stream = file.OpenReadStream();
        await using (stream.ConfigureAwait(false))
        {
            var read = await stream.ReadAtLeastAsync(head, head.Length, throwOnEndOfStream: false, cancellationToken)
                .ConfigureAwait(false);
            return FileSignature.DetectMediaType(head.AsSpan(0, read));
        }
    }

    private IResult Reject(string reason, int status, string title, long sizeBytes, IDictionary<string, object?>? extensions = null)
    {
        logger.UploadRejected(reason, sizeBytes);
        s_rejected.Add(1, new KeyValuePair<string, object?>("nomorepaper.upload.reason", reason));
        return Results.Problem(title: title, statusCode: status, extensions: extensions);
    }
}
