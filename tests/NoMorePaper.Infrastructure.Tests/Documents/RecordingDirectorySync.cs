using NoMorePaper.Infrastructure.Documents;

namespace NoMorePaper.Infrastructure.Tests.Documents;

/// <summary>Records each flushed folder with the file names it held at that moment.</summary>
internal sealed class RecordingDirectorySync : IDirectorySync
{
    private readonly List<(string Folder, string[] Files)> _flushes = [];

    public IReadOnlyList<(string Folder, string[] Files)> Flushes => _flushes;

    public void Flush(string folder) =>
        _flushes.Add((folder, [.. Directory.EnumerateFiles(folder).Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal)]));
}
