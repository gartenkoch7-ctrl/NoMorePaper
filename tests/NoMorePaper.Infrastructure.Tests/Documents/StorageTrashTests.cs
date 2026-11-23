using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Documents;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Documents;

/// <summary>The edited version, the trash folders and the page images of edits on disk (M4b).</summary>
public sealed class StorageTrashTests : IDisposable
{
    private static readonly DateTimeOffset s_received = new(2025, 3, 14, 9, 26, 53, TimeSpan.Zero);

    private readonly DirectoryInfo _sandbox = Directory.CreateTempSubdirectory("nomorepaper-trash-");
    private readonly string _root;
    private readonly FileSystemDocumentStorage _storage;
    private readonly EditPageStorage _pages;
    private readonly Guid _id = Guid.CreateVersion7(s_received);
    private readonly Guid _item = Guid.CreateVersion7();

    public StorageTrashTests()
    {
        _root = Path.Combine(_sandbox.FullName, "data");
        _storage = new FileSystemDocumentStorage(_root, new RecordingDirectorySync(), NullLogger<FileSystemDocumentStorage>.Instance);
        _pages = new EditPageStorage(_root, new RecordingDirectorySync(), NullLogger<EditPageStorage>.Instance);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string ArchiveFolder => Path.Combine(_root, "archive", "2025", "03", _id.ToString());

    private string TrashFolder => Path.Combine(_root, "trash", _item.ToString());

    public void Dispose() => _sandbox.Delete(recursive: true);

    [Fact]
    public async Task SaveEdited_WritesEditedPdf_NextToTheArchive_AndReplacesIt()
    {
        await _storage.SaveEditedAsync(_id, new MemoryStream("first"u8.ToArray()), Ct);
        var stored = await _storage.SaveEditedAsync(_id, new MemoryStream("second"u8.ToArray()), Ct);

        Assert.Equal("second"u8.ToArray(), await File.ReadAllBytesAsync(Path.Combine(ArchiveFolder, "edited.pdf"), Ct));
        Assert.Equal(6, stored.SizeBytes);
        await using var read = _storage.OpenRead(_id, DocumentFileKind.Edited);
        Assert.Equal(6, read.Length);
    }

    [Fact]
    public async Task TheOriginal_GoesToTheTrash_AndComesBack()
    {
        await ArchivedOriginalAsync();

        await _storage.MoveToTrashAsync(_id, DocumentFileKind.Original, _item, Ct);
        await _storage.MoveToTrashAsync(_id, DocumentFileKind.Original, _item, Ct);

        Assert.True(File.Exists(Path.Combine(TrashFolder, "original.jpg")));
        Assert.Throws<FileNotFoundException>(() => _storage.OpenRead(_id, DocumentFileKind.Original));

        await _storage.RestoreFromTrashAsync(_item, _id, DocumentFileKind.Original, Ct);
        await _storage.RestoreFromTrashAsync(_item, _id, DocumentFileKind.Original, Ct);

        Assert.True(File.Exists(Path.Combine(ArchiveFolder, "original.jpg")));
        Assert.False(Directory.Exists(TrashFolder));
    }

    [Fact]
    public async Task TheEditedVersion_GoesToTheTrash_AndIsPurgedForGood()
    {
        await _storage.SaveEditedAsync(_id, new MemoryStream("edited"u8.ToArray()), Ct);

        await _storage.MoveToTrashAsync(_id, DocumentFileKind.Edited, _item, Ct);
        Assert.True(File.Exists(Path.Combine(TrashFolder, "edited.pdf")));
        Assert.False(File.Exists(Path.Combine(ArchiveFolder, "edited.pdf")));

        _storage.PurgeTrash(_item);
        _storage.PurgeTrash(_item);
        Assert.False(Directory.Exists(TrashFolder));
    }

    [Fact]
    public async Task Restore_IsRefused_WhileThePlaceIsTaken_AndMissingFilesAreReported()
    {
        await _storage.SaveEditedAsync(_id, new MemoryStream("old"u8.ToArray()), Ct);
        await _storage.MoveToTrashAsync(_id, DocumentFileKind.Edited, _item, Ct);
        await _storage.SaveEditedAsync(_id, new MemoryStream("new"u8.ToArray()), Ct);

        await Assert.ThrowsAsync<IOException>(() => _storage.RestoreFromTrashAsync(_item, _id, DocumentFileKind.Edited, Ct));
        Assert.True(File.Exists(Path.Combine(TrashFolder, "edited.pdf")));
        await Assert.ThrowsAsync<FileNotFoundException>(() => _storage.MoveToTrashAsync(_id, DocumentFileKind.Original, Guid.CreateVersion7(), Ct));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _storage.MoveToTrashAsync(_id, DocumentFileKind.Archive, _item, Ct));
    }

    [Fact]
    public async Task EditPages_AreStoredPerPage_AndDiscardedTogether()
    {
        await _pages.SaveAsync(_id, 2, new MemoryStream([0xFF, 0xD8, 1]), Ct);
        await _pages.SaveAsync(_id, 2, new MemoryStream([0xFF, 0xD8, 2]), Ct);

        await using (var read = _pages.OpenRead(_id, 2))
        {
            using var copy = new MemoryStream();
            await read.CopyToAsync(copy, Ct);
            Assert.Equal([0xFF, 0xD8, 2], copy.ToArray());
        }

        Assert.Equal(["page-2.jpg"], Directory.GetFiles(Path.Combine(_root, "edits", _id.ToString())).Select(Path.GetFileName));
        Assert.Throws<FileNotFoundException>(() => _pages.OpenRead(_id, 3));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _pages.SaveAsync(_id, 0, new MemoryStream(), Ct));

        _pages.Discard(_id);
        _pages.Discard(_id);
        Assert.Throws<FileNotFoundException>(() => _pages.OpenRead(_id, 2));
    }

    [Fact]
    public async Task Delete_AlsoRemovesThePageImagesOfAnEdit()
    {
        await _pages.SaveAsync(_id, 1, new MemoryStream([1]), Ct);

        _storage.Delete(_id);

        Assert.False(Directory.Exists(Path.Combine(_root, "edits", _id.ToString())));
    }

    private async Task ArchivedOriginalAsync()
    {
        await _storage.SaveInboxAsync(_id, "Foto.jpg", new MemoryStream([1, 2, 3]), Ct);
        await _storage.ArchiveOriginalAsync(_id, Ct);
    }
}
