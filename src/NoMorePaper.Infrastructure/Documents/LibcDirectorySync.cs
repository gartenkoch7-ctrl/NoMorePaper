using System.Runtime.InteropServices;
using System.Text;

namespace NoMorePaper.Infrastructure.Documents;

/// <summary>
/// Flushes folders with <c>fsync</c> through libc, for Linux: .NET cannot open directories as files.
/// </summary>
internal sealed class LibcDirectorySync : IDirectorySync
{
    private const int ReadOnly = 0;

    // fsync on a directory is not supported by every file system (e.g. some network mounts); the
    // rename is then as durable as that file system makes it.
    private const int InvalidArgument = 22;
    private const int NotSupported = 95;

    public void Flush(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
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
