namespace NoMorePaper.Infrastructure.Documents;

/// <summary>
/// Makes a rename durable. On Linux a renamed file only survives a power loss once its folder is
/// flushed too (<c>fsync</c> on the directory); Windows (NTFS journals the rename) needs nothing.
/// The composition root picks the implementation for the operating system.
/// </summary>
public interface IDirectorySync
{
    /// <summary>
    /// Flushes the folder and its parent: the folder holds the renamed file, the parent the folder
    /// itself, which is usually new (<c>inbox/&lt;id&gt;</c>). Folders further up are new only once a
    /// month (<c>archive/&lt;yyyy&gt;/&lt;mm&gt;</c>) and are not flushed, to keep each write cheap.
    /// </summary>
    /// <exception cref="IOException">A folder could not be opened or flushed.</exception>
    void Flush(string folder);
}
