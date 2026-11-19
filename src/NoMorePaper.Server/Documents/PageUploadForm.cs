using NoMorePaper.Application.Documents;

namespace NoMorePaper.Server.Documents;

/// <summary>
/// The checked form of <c>POST /api/documents/pages</c>: 1–<see cref="MaxPages"/> JPEG parts named
/// <see cref="PageField"/>, each up to <see cref="MaxPageBytes"/>, together up to <see cref="MaxTotalBytes"/>.
/// The type is read from the first bytes (<see cref="FileSignature"/>), never from the claimed type.
/// </summary>
internal sealed class PageUploadForm
{
    public const int MaxPages = 50;
    public const long MaxPageBytes = 15L * 1024 * 1024;
    public const long MaxTotalBytes = 150L * 1024 * 1024;

    /// <summary>Room for the multipart framing around the pages.</summary>
    public const long MaxRequestBytes = MaxTotalBytes + (1024 * 1024);

    public const string PageField = "page";
    public const string PrivateField = "private";
    public const string TakenAtField = "takenAt";

    private PageUploadForm(IReadOnlyList<IFormFile> pages, bool isPrivate, string? takenAt)
    {
        Pages = pages;
        IsPrivate = isPrivate;
        TakenAt = takenAt;
        SizeBytes = pages.Sum(page => page.Length);
    }

    /// <summary>The pages in the order the client sent them.</summary>
    public IReadOnlyList<IFormFile> Pages { get; }

    public bool IsPrivate { get; }

    /// <summary>Raw field <c>takenAt</c>; see <see cref="PageUploadName.For"/>.</summary>
    public string? TakenAt { get; }

    public long SizeBytes { get; }

    /// <returns>The checked form, or why it is refused (fixed reason word, HTTP status, ProblemDetails title).</returns>
    public static async Task<(PageUploadForm? Form, PageUploadRefusal? Refusal)> CheckAsync(
        IFormCollection form, CancellationToken cancellationToken)
    {
        // Anything but true or false is refused: a typo must not silently share a private document.
        var privateValues = form[PrivateField];
        var isPrivate = false;
        if (privateValues.Count > 0 && (privateValues.Count != 1 || !bool.TryParse(privateValues[0], out isPrivate)))
        {
            return (null, new("malformed", StatusCodes.Status400BadRequest, "Field 'private' must be true or false."));
        }

        var pages = form.Files.GetFiles(PageField);
        if (pages.Count == 0 || pages.Any(page => page.Length == 0))
        {
            return (null, new("empty", StatusCodes.Status400BadRequest, "Send 1 to 50 non-empty parts named 'page'."));
        }

        if (pages.Count > MaxPages)
        {
            return (null, new("too-many-pages", StatusCodes.Status400BadRequest, "At most 50 pages make one document."));
        }

        if (pages.Any(page => page.Length > MaxPageBytes) || pages.Sum(page => page.Length) > MaxTotalBytes)
        {
            return (null, new("too-large", StatusCodes.Status413PayloadTooLarge, "The pages are too large."));
        }

        foreach (var page in pages)
        {
            if (await DetectAsync(page, cancellationToken).ConfigureAwait(false) != "image/jpeg")
            {
                return (null, new("type-not-allowed", StatusCodes.Status400BadRequest, "Every page must be a JPEG image."));
            }
        }

        var takenAt = form[TakenAtField] is { Count: 1 } values ? values[0] : null;
        return (new PageUploadForm(pages, isPrivate, takenAt), null);
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
}
