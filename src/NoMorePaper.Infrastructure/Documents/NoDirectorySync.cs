namespace NoMorePaper.Infrastructure.Documents;

/// <summary>For systems whose file system makes a rename durable by itself (Windows, NTFS journal).</summary>
internal sealed class NoDirectorySync : IDirectorySync
{
    public void Flush(string folder) => ArgumentException.ThrowIfNullOrWhiteSpace(folder);
}
