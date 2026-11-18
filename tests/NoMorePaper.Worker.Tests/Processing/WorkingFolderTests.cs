using NoMorePaper.Worker.Processing;
using Xunit;

namespace NoMorePaper.Worker.Tests.Processing;

public sealed class WorkingFolderTests : IDisposable
{
    private static readonly DateTimeOffset s_now = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);
    private readonly DirectoryInfo _dataRoot = Directory.CreateTempSubdirectory("nomorepaper-working-folder-");
    private readonly RecordingLogger<WorkingFolder> _log = new();

    private string Root => Path.Combine(_dataRoot.FullName, WorkingFolder.FolderName);

    [Fact]
    public void Prepare_CreatesTheFolder()
    {
        Folder().Prepare();

        Assert.True(Directory.Exists(Root));
        Assert.Empty(_log.Entries);
    }

    [Fact]
    public void Prepare_RemovesLeftoversOlderThanADay_AndKeepsYoungerOnes()
    {
        Directory.CreateDirectory(Root);
        var oldFolder = Leftover("attempt-old", isFolder: true, s_now - TimeSpan.FromDays(2));
        var oldFile = Leftover("tmpabc.pdf", isFolder: false, s_now - WorkingFolder.LeftoverAge - TimeSpan.FromMinutes(1));
        var youngFolder = Leftover("attempt-young", isFolder: true, s_now - TimeSpan.FromHours(1));

        Folder().Prepare();

        Assert.False(Directory.Exists(oldFolder));
        Assert.False(File.Exists(oldFile));
        Assert.True(Directory.Exists(youngFolder));
        Assert.Equal([2112], _log.EventIds);
    }

    [Fact]
    public void CreateAttemptFolder_ReturnsANewEmptyFolder_BelowTheRoot()
    {
        var folder = Folder();

        var first = new DirectoryInfo(folder.CreateAttemptFolder());
        var second = new DirectoryInfo(folder.CreateAttemptFolder());

        Assert.NotEqual(first.FullName, second.FullName);
        Assert.Equal(folder.Root, first.Parent?.FullName);
        Assert.Empty(first.GetFileSystemInfos());
    }

    public void Dispose() => _dataRoot.Delete(recursive: true);

    private WorkingFolder Folder() => new(Root, new ManualTimeProvider(s_now), _log);

    private string Leftover(string name, bool isFolder, DateTimeOffset lastWrite)
    {
        var path = Path.Combine(Root, name);
        if (isFolder)
        {
            Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, "original"), "copy");
            Directory.SetLastWriteTimeUtc(path, lastWrite.UtcDateTime);
        }
        else
        {
            File.WriteAllText(path, "temp");
            File.SetLastWriteTimeUtc(path, lastWrite.UtcDateTime);
        }

        return path;
    }
}
