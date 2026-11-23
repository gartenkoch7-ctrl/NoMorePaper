namespace NoMorePaper.Application.Editing;

/// <summary>
/// Page images the browser cropped or straightened (ADR-0025), kept below <c>edits/&lt;id&gt;/</c> until
/// the Worker has carried out the edit. Writes are atomic like those of the document storage.
/// </summary>
public interface IEditPageStorage
{
    /// <summary>Stores the JPEG that replaces <paramref name="page"/> of the source, replacing an earlier one.</summary>
    /// <param name="page">Page of the current source, starting at 1.</param>
    Task SaveAsync(Guid id, int page, Stream jpeg, CancellationToken cancellationToken);

    /// <summary>Opens the stored image of <paramref name="page"/>.</summary>
    /// <exception cref="FileNotFoundException">No image is stored for that page.</exception>
    Stream OpenRead(Guid id, int page);

    /// <summary>Removes every stored image of the document; does nothing when there is none.</summary>
    /// <exception cref="IOException">The folder could not be removed.</exception>
    /// <exception cref="UnauthorizedAccessException">The folder could not be removed.</exception>
    void Discard(Guid id);
}
