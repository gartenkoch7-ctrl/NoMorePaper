using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Documents;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Documents;

/// <summary>Every rename is followed by a flush of the folder that now holds the file.</summary>
public sealed class FileSystemDocumentStorageSyncTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("nomorepaper-storage-sync-");
    private readonly RecordingDirectorySync _directories = new();
    private readonly FileSystemDocumentStorage _storage;
    private readonly Guid _id = Guid.CreateVersion7(new DateTimeOffset(2025, 3, 14, 9, 26, 53, TimeSpan.Zero));

    public FileSystemDocumentStorageSyncTests() =>
        _storage = new FileSystemDocumentStorage(_root.FullName, _directories, NullLogger<FileSystemDocumentStorage>.Instance);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string InboxFolder => Path.Combine(_root.FullName, "inbox", _id.ToString());

    private string ArchiveFolder => Path.Combine(_root.FullName, "archive", "2025", "03", _id.ToString());

    public void Dispose() => _root.Delete(recursive: true);

    [Fact]
    public async Task SaveInbox_FlushesTheFolder_AfterTheRename()
    {
        await _storage.SaveInboxAsync(_id, "a.pdf", new MemoryStream([1, 2, 3]), Ct);

        var flush = Assert.Single(_directories.Flushes);
        Assert.Equal(InboxFolder, flush.Folder);
        Assert.Equal(["original.pdf"], flush.Files);
    }

    [Fact]
    public async Task SaveArchive_FlushesTheFolder_AfterTheRename()
    {
        await _storage.SaveArchiveAsync(_id, DocumentFileKind.Archive, new MemoryStream([1, 2, 3]), Ct);

        var flush = Assert.Single(_directories.Flushes);
        Assert.Equal(ArchiveFolder, flush.Folder);
        Assert.Equal(["archive.pdf"], flush.Files);
    }

    [Fact]
    public async Task ArchiveOriginal_FlushesTheArchiveFolder_AfterTheMove()
    {
        await _storage.SaveInboxAsync(_id, "a.pdf", new MemoryStream([1, 2, 3]), Ct);

        await _storage.ArchiveOriginalAsync(_id, Ct);

        Assert.Equal(2, _directories.Flushes.Count);
        Assert.Equal(ArchiveFolder, _directories.Flushes[1].Folder);
        Assert.Equal(["original.pdf"], _directories.Flushes[1].Files);
    }
}
