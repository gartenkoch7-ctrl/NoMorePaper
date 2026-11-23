namespace NoMorePaper.Application.Editing;

/// <summary>A JPEG the browser sent for a cropped or straightened page (<see cref="RequestPageEdit"/>).</summary>
/// <param name="Page">The page of the current version it replaces, starting at 1.</param>
/// <param name="Length">Its size in bytes, as received.</param>
/// <param name="Open">Opens its bytes from the start; may be called more than once.</param>
public sealed record EditPageImage(int Page, long Length, Func<Stream> Open)
{
    /// <summary>At most this many replaced pages per edit.</summary>
    public const int MaxImages = 50;

    /// <summary>At most this size per image (as for camera pages).</summary>
    public const long MaxBytes = 15L * 1024 * 1024;
}
