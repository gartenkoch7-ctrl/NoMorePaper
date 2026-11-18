using System.Runtime.InteropServices;
using System.Text;

namespace NoMorePaper.Infrastructure.Documents;

/// <summary>
/// Makes a rename durable. On Linux a renamed file only survives a power loss once its folder is
/// flushed too (<c>fsync</c> on the directory); .NET cannot open directories as files, so this calls
/// libc. Windows (NTFS journals the rename) and other systems need nothing: there it does nothing.
/// </summary>
internal sealed class DirectorySync
{
    private const int ReadOnly = 0;

    // fsync on a directory is not supported by every file system (e.g. some network mounts); the
    // rename is then as durable as that file system makes it.
    private const int InvalidArgument = 22;
    private const int NotSupported = 95;

    private readonly bool _needed = OperatingSystem.IsLinux();

    /// <summary>
    /// Flushes the folder and its parent: the folder holds the renamed file, the parent the folder
    /// itself, which is usually new (<c>inbox/&lt;id&gt;</c>). Folders further up are new only once a
    /// month (<c>archive/&lt;yyyy&gt;/&lt;mm&gt;</c>) and are not flushed, to keep each write cheap.
    /// </summary>
    /// <exception cref="IOException">A folder could not be opened or flushed.</exception>
    public void Flush(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        if (!_needed)
        {
            return;
        }

        FlushOne(folder);
        if (Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(folder)) is { Length: > 0 } parent)
        {
            FlushOne(parent);
        }
    }

    private static void FlushOne(string folder)
    {
        // NUL-terminated UTF-8, as open(2) expects; passing bytes avoids string marshalling.
        var handle = Open(Encoding.UTF8.GetBytes(folder + '\0'), ReadOnly);
        if (handle < 0)
        {
            throw new IOException($"Could not open the folder to flush it (errno {Marshal.GetLastPInvokeError()}).");
        }

        try
        {
            if (FSync(handle) != 0 && Marshal.GetLastPInvokeError() is var error && error is not (InvalidArgument or NotSupported))
            {
                throw new IOException($"Could not flush the folder (errno {error}).");
            }
        }
        finally
        {
            _ = Close(handle);
        }
    }

    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    private static extern int Open(byte[] path, int flags);

    [DllImport("libc", EntryPoint = "fsync", SetLastError = true)]
    private static extern int FSync(int handle);

    [DllImport("libc", EntryPoint = "close", SetLastError = true)]
    private static extern int Close(int handle);
}
