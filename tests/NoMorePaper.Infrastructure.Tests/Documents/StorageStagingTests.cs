using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Documents;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Documents;

/// <summary>
/// Files an edit stages (M4b): apart from the files in use until published, every move safe to repeat, and
/// gone with the edit's folder.
/// </summary>
public sealed class StorageStagingTests : IDisposable
{
    private static readonly DateTimeOffset s_received = new(2025, 3, 14, 9, 26, 53, TimeSpan.Zero);

    private readonly DirectoryInfo _sandbox = Directory.CreateTempSubdirectory("nomorepaper-staging-");
    private readonly string _root;
    private readonly FileSystemDocumentStorage _storage;
    private readonly Guid _id = Guid.CreateVersion7(s_received);
    private readonly Guid _edit = Guid.CreateVersion7();
    private readonly Guid _item = Guid.CreateVersion7();

    public StorageStagingTests()
    {
        _root = Path.Combine(_sandbox.FullName, "data");
        _storage = new FileSystemDocumentStorage(_root, new RecordingDirectorySync(), NullLogger<FileSystemDocumentStorage>.Instance);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string ArchiveFolder => Path.Combine(_root, "archive", "2025", "03", _id.ToString());

    public void Dispose() => _sandbox.Delete(recursive: true);

    [Fact]
    public async Task AStagedFile_StaysApart_UntilPublished_AndPublishingTwice_ChangesNothing()
    {
        await _storage.SaveArchiveAsync(_id, DocumentFileKind.Archive, new MemoryStream("old"u8.ToArray()), Ct);

        var staged = await _storage.StageAsync(_id, _edit, DocumentFileKind.Archive, new MemoryStream("new"u8.ToArray()), Ct);

        Assert.Equal(3, staged.SizeBytes);
        Assert.True(_storage.IsStaged(_id, _edit, DocumentFileKind.Archive));
        Assert.False(_storage.IsStaged(_id, _edit, DocumentFileKind.Edited));
        Assert.Equal("old", await ReadAsync(DocumentFileKind.Archive));

        await _storage.PublishStagedAsync(_id, _edit, DocumentFileKind.Archive, Ct);
        await _storage.PublishStagedAsync(_id, _edit, DocumentFileKind.Archive, Ct);

        Assert.Equal("new", await ReadAsync(DocumentFileKind.Archive));
        Assert.False(_storage.IsStaged(_id, _edit, DocumentFileKind.Archive));
    }

    [Fact]
    public async Task AStagedEditedVersion_GoesStraightToTheTrash_Once()
    {
        await _storage.StageAsync(_id, _edit, DocumentFileKind.Edited, new MemoryStream("edited"u8.ToArray()), Ct);

        await _storage.TrashStagedAsync(_id, _edit, _item, Ct);
        await _storage.TrashStagedAsync(_id, _edit, _item, Ct);

        Assert.Equal("edited", await File.ReadAllTextAsync(Path.Combine(_root, "trash", _item.ToString(), "edited.pdf"), Ct));
        Assert.False(File.Exists(Path.Combine(ArchiveFolder, "edited.pdf")));
    }

    [Fact]
    public async Task TheOriginal_IsNeverStaged()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _storage.StageAsync(_id, _edit, DocumentFileKind.Original, new MemoryStream([1]), Ct));
        Assert.Throws<ArgumentOutOfRangeException>(() => _storage.IsStaged(_id, _edit, DocumentFileKind.Original));
    }

    private async Task<string> ReadAsync(DocumentFileKind kind)
    {
        await using var stream = _storage.OpenRead(_id, kind);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(Ct);
    }
}
