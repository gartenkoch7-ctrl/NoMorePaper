namespace NoMorePaper.Worker.Processing;

/// <summary>
/// Where processing attempts keep their working copies and the OCR tools their temporary files:
/// <c>tmp</c> on the data volume. Not <c>/tmp</c>, which is RAM (tmpfs) in the container, while a
/// scan and its intermediate images can take hundreds of megabytes.
/// </summary>
internal sealed class WorkingFolder(string root, TimeProvider time, ILogger<WorkingFolder> logger)
{
    /// <summary>Folder name below the data root.</summary>
    public const string FolderName = "tmp";

    /// <summary>Leftovers younger than this may still be in use by another Worker sharing the volume.</summary>
    public static readonly TimeSpan LeftoverAge = TimeSpan.FromDays(1);

    public string Root { get; } = Path.GetFullPath(root);

    /// <summary>
    /// Creates the folder and removes what earlier attempts left behind when the Worker was killed
    /// (crash, out of memory); an attempt that ends normally deletes its own folder.
    /// </summary>
    public void Prepare()
    {
        var folder = Directory.CreateDirectory(Root);
        var cutoff = time.GetUtcNow().UtcDateTime - LeftoverAge;
        var removed = 0;
        foreach (var entry in folder.EnumerateFileSystemInfos())
        {
            if (entry.LastWriteTimeUtc >= cutoff)
            {
                continue;
            }

            try
            {
                if (entry is DirectoryInfo directory)
                {
                    directory.Delete(recursive: true);
                }
                else
                {
                    entry.Delete();
                }

                removed++;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LeftoverNotRemoved(exception.GetType().Name);
            }
        }

        if (removed > 0)
        {
            logger.LeftoversRemoved(removed);
        }
    }

    /// <summary>A new, empty folder for one attempt.</summary>
    public DirectoryInfo CreateAttemptFolder() =>
        Directory.CreateDirectory(Path.Combine(Root, $"attempt-{Guid.NewGuid():N}"));
}
