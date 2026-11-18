using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Documents;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Documents;

public sealed class FileSystemDocumentStorageTests : IDisposable
{
    private static readonly DateTimeOffset s_received = new(2025, 3, 14, 9, 26, 53, TimeSpan.Zero);
    private static readonly byte[] s_content = [.. Enumerable.Range(0, 200_000).Select(i => (byte)(i % 251))];

    private readonly DirectoryInfo _sandbox = Directory.CreateTempSubdirectory("nomorepaper-storage-");
    private readonly string _root;
    private readonly FileSystemDocumentStorage _storage;
    private readonly Guid _id = Guid.CreateVersion7(s_received);

    public FileSystemDocumentStorageTests()
    {
        // The root is one level down, so files escaping it would still land inside the sandbox.
        _root = Path.Combine(_sandbox.FullName, "data");
        _storage = new FileSystemDocumentStorage(_root, new RecordingDirectorySync(), NullLogger<FileSystemDocumentStorage>.Instance);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string InboxFolder => Path.Combine(_root, "inbox", _id.ToString());

    private string ArchiveFolder => Path.Combine(_root, "archive", "2025", "03", _id.ToString());

    public void Dispose() => _sandbox.Delete(recursive: true);

    [Fact]
    public async Task SaveInbox_WritesTheOriginal_AndReturnsSizeAndHash()
    {
        var stored = await _storage.SaveInboxAsync(_id, "Scan 2025.PDF", new MemoryStream(s_content), Ct);

        var path = Path.Combine(InboxFolder, "original.pdf");
        Assert.Equal(s_content, await File.ReadAllBytesAsync(path, Ct));
        Assert.Equal(s_content.Length, stored.SizeBytes);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(s_content)), stored.Sha256);
        Assert.Equal([path], AllFiles());
    }

    [Theory]
    [InlineData("../../../escape.exe", "original.exe")]
    [InlineData(@"..\..\escape.jpg", "original.jpg")]
    [InlineData("/etc/passwd", "original.bin")]
    [InlineData("no-extension", "original.bin")]
    [InlineData("", "original.bin")]
    [InlineData("archive.tar.gz", "original.gz")]
    [InlineData("name.extensiontoolong", "original.bin")]
    [InlineData("name.p%2Fdf", "original.bin")]
    [InlineData("name.HEIC", "original.heic")]
    public async Task SaveInbox_UsesOnlyACleanedExtension_OfTheReceivedName(string fileName, string expected)
    {
        await _storage.SaveInboxAsync(_id, fileName, new MemoryStream(s_content), Ct);

        Assert.Equal([Path.Combine(InboxFolder, expected)], AllFiles());
    }

    [Fact]
    public async Task SaveInbox_AgainWithAnotherExtension_KeepsOnlyTheNewOriginal()
    {
        await _storage.SaveInboxAsync(_id, "a.jpg", new MemoryStream([1, 2, 3]), Ct);
        await _storage.SaveInboxAsync(_id, "a.png", new MemoryStream([4, 5]), Ct);

        Assert.Equal([Path.Combine(InboxFolder, "original.png")], AllFiles());
    }

    [Fact]
    public async Task SaveArchive_WritesArchivePdf_UnderTheYearAndMonthOfTheId()
    {
        var stored = await _storage.SaveArchiveAsync(_id, DocumentFileKind.Archive, new MemoryStream(s_content), Ct);

        Assert.Equal([Path.Combine(ArchiveFolder, "archive.pdf")], AllFiles());
        Assert.Equal(s_content.Length, stored.SizeBytes);
    }

    [Fact]
    public async Task SaveArchive_RejectsOriginals() =>
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _storage.SaveArchiveAsync(_id, DocumentFileKind.Original, new MemoryStream(s_content), Ct));

    [Fact]
    public async Task Save_RejectsIdsThatAreNotVersion7()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _storage.SaveInboxAsync(Guid.NewGuid(), "a.pdf", new MemoryStream(), Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => _storage.SaveInboxAsync(Guid.Empty, "a.pdf", new MemoryStream(), Ct));
    }

    [Fact]
    public async Task Save_LeavesNoFile_WhenTheSourceFailsHalfway()
    {
        await Assert.ThrowsAsync<IOException>(
            () => _storage.SaveInboxAsync(_id, "a.pdf", new FailingStream(s_content, failAfterBytes: 100_000), Ct));

        Assert.Empty(AllFiles());
    }

    [Fact]
    public async Task Save_LeavesNoFile_WhenCancelledHalfway()
    {
        using var cancellation = new CancellationTokenSource();
        var source = new FailingStream(s_content, failAfterBytes: int.MaxValue) { OnRead = cancellation.Cancel };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _storage.SaveArchiveAsync(_id, DocumentFileKind.Archive, source, cancellation.Token));

        Assert.Empty(AllFiles());
    }

    [Fact]
    public async Task Save_KeepsTheOldFile_WhenAReplacementFails()
    {
        await _storage.SaveArchiveAsync(_id, DocumentFileKind.Archive, new MemoryStream([7, 7, 7]), Ct);

        await Assert.ThrowsAsync<IOException>(
            () => _storage.SaveArchiveAsync(_id, DocumentFileKind.Archive, new FailingStream(s_content, failAfterBytes: 10), Ct));

        Assert.Equal([7, 7, 7], await ReadAsync(DocumentFileKind.Archive));
        Assert.Single(AllFiles());
    }

    [Fact]
    public async Task OpenRead_FindsTheOriginal_InTheInboxAndAfterArchiving()
    {
        await _storage.SaveInboxAsync(_id, "a.jpg", new MemoryStream(s_content), Ct);
        Assert.Equal(s_content, await ReadAsync(DocumentFileKind.Original));

        await _storage.ArchiveOriginalAsync(_id, Ct);

        Assert.Equal(s_content, await ReadAsync(DocumentFileKind.Original));
        Assert.Equal([Path.Combine(ArchiveFolder, "original.jpg")], AllFiles());
        Assert.False(Directory.Exists(InboxFolder));
    }

    [Fact]
    public async Task ArchiveOriginal_CanBeRepeated()
    {
        await _storage.SaveInboxAsync(_id, "a.pdf", new MemoryStream(s_content), Ct);

        await _storage.ArchiveOriginalAsync(_id, Ct);
        await _storage.ArchiveOriginalAsync(_id, Ct);

        Assert.Equal([Path.Combine(ArchiveFolder, "original.pdf")], AllFiles());
    }

    [Fact]
    public async Task ArchiveOriginal_Fails_WhenNoOriginalExists() =>
        await Assert.ThrowsAsync<FileNotFoundException>(() => _storage.ArchiveOriginalAsync(_id, Ct));

    [Theory]
    [InlineData(DocumentFileKind.Original)]
    [InlineData(DocumentFileKind.Archive)]
    public void OpenRead_Fails_WhenTheFileIsMissing(DocumentFileKind kind) =>
        Assert.Throws<FileNotFoundException>(() => _storage.OpenRead(_id, kind));

    [Fact]
    public async Task Save_WhileTheFileIsBeingRead_ReplacesItOnLinux_AndLeavesItIntactOnWindows()
    {
        await _storage.SaveArchiveAsync(_id, DocumentFileKind.Archive, new MemoryStream([1, 1]), Ct);
        await using (var reader = _storage.OpenRead(_id, DocumentFileKind.Archive))
        {
            var replace = () => _storage.SaveArchiveAsync(_id, DocumentFileKind.Archive, new MemoryStream([2, 2, 2]), Ct);
            if (OperatingSystem.IsWindows())
            {
                // Windows refuses to replace an open file even with FileShare.Delete; production runs on Linux.
                await Assert.ThrowsAsync<UnauthorizedAccessException>(replace);
            }
            else
            {
                await replace();
            }

            Assert.Equal(1, reader.ReadByte());
        }

        Assert.Equal(OperatingSystem.IsWindows() ? [1, 1] : [2, 2, 2], await ReadAsync(DocumentFileKind.Archive));
        Assert.Single(AllFiles());
    }

    private async Task<byte[]> ReadAsync(DocumentFileKind kind)
    {
        await using var stream = _storage.OpenRead(_id, kind);
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy, Ct);
        return copy.ToArray();
    }

    private string[] AllFiles() =>
        [.. Directory.EnumerateFiles(_sandbox.FullName, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal)];
}
