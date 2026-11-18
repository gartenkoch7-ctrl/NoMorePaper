using Microsoft.Extensions.Logging;

namespace NoMorePaper.Infrastructure.Documents;

/// <summary>
/// Removes leftovers of the document storage (temporary files, superseded originals, empty inbox
/// folders). Runs after the stored state is already correct, so a failure is logged, not thrown.
/// </summary>
internal sealed class StorageCleanup(ILogger logger)
{
    /// <summary>An earlier original with another extension would make the original ambiguous.</summary>
    public void RemoveOtherOriginals(Guid id, string area, string keep)
    {
        var folder = Path.GetDirectoryName(keep)!;
        foreach (var other in Directory.EnumerateFiles(folder, DocumentPaths.OriginalPattern))
        {
            if (!string.Equals(other, keep, StringComparison.Ordinal))
            {
                TryCleanUp(id, area, () => File.Delete(other));
            }
        }
    }

    public void TryCleanUp(Guid id, string area, Action cleanUp)
    {
        try
        {
            cleanUp();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The stored state is already correct; a leftover only costs space.
            logger.CleanupFailed(id, area, exception.GetType().Name);
        }
    }
}
