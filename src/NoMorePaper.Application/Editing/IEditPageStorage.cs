namespace NoMorePaper.Application.Editing;

/// <summary>
/// Page images the browser cropped or straightened (ADR-0025), kept below <c>edits/&lt;id&gt;/&lt;editId&gt;/</c>
/// until the Worker has carried out that edit. Each edit has a folder of its own, so a refused or later
/// request never touches the images of an edit that waits or runs. Writes are atomic like those of the
/// document storage.
/// </summary>
public interface IEditPageStorage
{
    /// <summary>Stores the JPEG that replaces <paramref name="page"/> of the source, replacing an earlier one of the same edit.</summary>
    /// <param name="editId">The edit (<see cref="EditData.EditId"/>).</param>
    /// <param name="page">Page of the current source, starting at 1.</param>
    Task SaveAsync(Guid id, Guid editId, int page, Stream jpeg, CancellationToken cancellationToken);

    /// <summary>Opens the stored image of <paramref name="page"/>.</summary>
    /// <exception cref="FileNotFoundException">No image is stored for that page.</exception>
    Stream OpenRead(Guid id, Guid editId, int page);

    /// <summary>
    /// Removes the folder of the edit with everything in it (its page images and staged files); does nothing
    /// when there is none.
    /// </summary>
    /// <exception cref="IOException">The folder could not be removed.</exception>
    /// <exception cref="UnauthorizedAccessException">The folder could not be removed.</exception>
    void Discard(Guid id, Guid editId);
}
