namespace NoMorePaper.Infrastructure.Inputs.ScannerFolder;

/// <summary>
/// Tells when a file in the scanner folder is completely written: a scanner writes over SMB for seconds and
/// nothing marks the end. A file is finished when size and change time are the same in two runs in a row
/// and nobody else has it open. Singleton: the memory spans the runs, each of which has a scope of its own;
/// after a restart every file waits one run more.
/// </summary>
public sealed class StableFiles
{
    private readonly Dictionary<string, Snapshot> _seen = new(StringComparer.Ordinal);
    private readonly Lock _lock = new();

    /// <summary>Notes how the file looks now and tells whether it looked the same at the previous run.</summary>
    /// <returns>
    /// <c>true</c> if finished; <c>false</c> on the first sight, after a change, while empty, or while another
    /// program holds it (on Linux only programs that lock it, which Samba by default does not).
    /// </returns>
    public bool IsFinished(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists)
        {
            Forget(path);
            return false;
        }

        var now = new Snapshot(info.Length, info.LastWriteTimeUtc);
        bool unchanged;
        lock (_lock)
        {
            unchanged = _seen.TryGetValue(path, out var before) && before == now;
            _seen[path] = now;
        }

        // An empty file is a scan that has not started writing yet (some scanners create the file first and
        // send the pages after scanning them all); moving it to the error folder would misfile the scan.
        return unchanged && now.Length > 0 && IsFree(path);
    }

    /// <summary>Forgets a file that was taken in, moved or deleted, so a new file under its name starts over.</summary>
    public void Forget(string path)
    {
        lock (_lock)
        {
            _seen.Remove(path);
        }
    }

    /// <summary>Forgets every file not seen in this run, so the memory does not grow.</summary>
    public void KeepOnly(IReadOnlyCollection<string> present)
    {
        ArgumentNullException.ThrowIfNull(present);
        var keep = present.ToHashSet(StringComparer.Ordinal);
        lock (_lock)
        {
            foreach (var path in _seen.Keys.Where(path => !keep.Contains(path)).ToList())
            {
                _seen.Remove(path);
            }
        }
    }

    // A writer that still holds the file (a slow copy, a paused scan) makes the exclusive open fail.
    private static bool IsFree(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private readonly record struct Snapshot(long Length, DateTime LastWriteUtc);
}
