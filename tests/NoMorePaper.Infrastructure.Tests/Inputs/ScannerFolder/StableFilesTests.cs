using NoMorePaper.Infrastructure.Inputs.ScannerFolder;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Inputs.ScannerFolder;

/// <summary>When a file in the scanner folder counts as completely written.</summary>
public sealed class StableFilesTests : IDisposable
{
    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("nomorepaper-stable-");
    private readonly StableFiles _stable = new();

    public void Dispose() => _folder.Delete(recursive: true);

    [Fact]
    public void AFile_IsFinished_OnlyWhenItLookedTheSameInTheRunBefore()
    {
        var path = Write("scan.pdf", "%PDF-1");

        Assert.False(_stable.IsFinished(path));
        Assert.True(_stable.IsFinished(path));
    }

    [Fact]
    public void AFileThatGrew_StartsOver()
    {
        var path = Write("scan.pdf", "%PDF-1");
        Assert.False(_stable.IsFinished(path));

        File.AppendAllText(path, " more pages");

        Assert.False(_stable.IsFinished(path));
        Assert.True(_stable.IsFinished(path));
    }

    [Fact]
    public void AFileAWriterStillHolds_IsNotFinished()
    {
        var path = Write("scan.pdf", "%PDF-1");
        Assert.False(_stable.IsFinished(path));

        using (new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.Read))
        {
            Assert.False(_stable.IsFinished(path));
        }

        Assert.True(_stable.IsFinished(path));
    }

    [Fact]
    public void ForgottenAndUnseenFiles_StartOver()
    {
        var first = Write("a.pdf", "%PDF-1");
        var second = Write("b.pdf", "%PDF-2");
        _stable.IsFinished(first);
        _stable.IsFinished(second);

        _stable.Forget(first);
        _stable.KeepOnly([first]);

        Assert.False(_stable.IsFinished(first));
        Assert.False(_stable.IsFinished(second));
    }

    [Fact]
    public void AnEmptyFile_IsNeverFinished()
    {
        var path = Write("scan.pdf", string.Empty);

        Assert.False(_stable.IsFinished(path));
        Assert.False(_stable.IsFinished(path));
    }

    [Fact]
    public void AMissingFile_IsNotFinished()
    {
        var path = Path.Combine(_folder.FullName, "gone.pdf");

        Assert.False(_stable.IsFinished(path));
        Assert.False(_stable.IsFinished(path));
    }

    private string Write(string name, string content)
    {
        var path = Path.Combine(_folder.FullName, name);
        File.WriteAllText(path, content);
        return path;
    }
}
