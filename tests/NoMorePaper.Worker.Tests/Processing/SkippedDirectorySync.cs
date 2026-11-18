using NoMorePaper.Infrastructure.Documents;

namespace NoMorePaper.Worker.Tests.Processing;

/// <summary>Durability of renames is not what these tests check; the storage tests cover it.</summary>
internal sealed class SkippedDirectorySync : IDirectorySync
{
    public void Flush(string folder)
    {
    }
}
