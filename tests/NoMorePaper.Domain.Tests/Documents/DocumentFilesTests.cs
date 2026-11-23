using NoMorePaper.Domain.Documents;
using Xunit;

namespace NoMorePaper.Domain.Tests.Documents;

/// <summary>Original, edited version and archive (ADR-0025): what may be replaced and removed.</summary>
public sealed class DocumentFilesTests
{
    private const string Sha = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
    private static readonly DateTimeOffset s_at = new(2026, 11, 20, 9, 0, 0, TimeSpan.FromHours(1));
    private static readonly Guid s_owner = Guid.CreateVersion7();

    [Fact]
    public void CurrentSource_IsTheEditedVersion_ElseTheOriginal()
    {
        var document = Done();
        Assert.Equal(DocumentFileKind.Original, document.CurrentSource?.Kind);

        document.ReplaceFile(File(DocumentFileKind.Edited), s_at);

        Assert.Equal(DocumentFileKind.Edited, document.CurrentSource?.Kind);
        Assert.Null(Document.Receive("Leer", s_at, s_owner, isPrivate: false).CurrentSource);
    }

    [Fact]
    public void ReplaceFile_ReplacesEditedAndArchive_AndRecordsWhenEdited()
    {
        var document = Done();
        var first = File(DocumentFileKind.Edited);
        var second = File(DocumentFileKind.Edited);
        var archive = File(DocumentFileKind.Archive);

        document.ReplaceFile(first, s_at);
        document.ReplaceFile(second, s_at.AddHours(1));
        document.ReplaceFile(archive, s_at);

        Assert.Same(second, document.FindFile(DocumentFileKind.Edited));
        Assert.Same(archive, document.FindFile(DocumentFileKind.Archive));
        Assert.Equal(3, document.Files.Count);
        Assert.Equal(s_at.AddHours(1), document.EditedAt);
        Assert.Equal(TimeSpan.Zero, document.EditedAt?.Offset);
    }

    [Fact]
    public void ReplaceFile_NeverReplacesTheOriginal() =>
        Assert.Throws<ArgumentException>(() => Done().ReplaceFile(File(DocumentFileKind.Original), s_at));

    [Fact]
    public void RemoveFile_KeepsASource()
    {
        var document = Done();
        Assert.Throws<InvalidOperationException>(() => document.RemoveFile(DocumentFileKind.Original));
        Assert.Throws<InvalidOperationException>(() => document.RemoveFile(DocumentFileKind.Edited));

        document.ReplaceFile(File(DocumentFileKind.Edited), s_at);
        document.RemoveFile(DocumentFileKind.Original);

        Assert.Null(document.FindFile(DocumentFileKind.Original));
        Assert.Throws<InvalidOperationException>(() => document.RemoveFile(DocumentFileKind.Edited));
    }

    [Fact]
    public void RemoveFile_OfTheEditedVersion_ForgetsWhenItWasMade()
    {
        var document = Done();
        document.ReplaceFile(File(DocumentFileKind.Edited), s_at);

        document.RemoveFile(DocumentFileKind.Edited);

        Assert.Null(document.EditedAt);
        Assert.Equal(DocumentFileKind.Original, document.CurrentSource?.Kind);
    }

    [Fact]
    public void ReplaceContent_OnlyForADoneDocument_KeepsTheFields()
    {
        var document = Done();
        document.Classify(new DocumentFields("Rechnung", null, null, null, null, "Kurz.", []));

        document.ReplaceContent("neuer Text");

        Assert.Equal(("neuer Text", "Rechnung", RecognitionState.Automatic), (document.ContentText, document.Title, document.Recognition));
        var queued = Document.Receive("Neu", s_at, s_owner, isPrivate: false);
        Assert.Throws<InvalidOperationException>(() => queued.ReplaceContent("x"));
    }

    private static Document Done()
    {
        var document = Document.Receive("Scan", s_at, s_owner, isPrivate: false);
        document.AttachFile(File(DocumentFileKind.Original));
        document.StartProcessing();
        document.Complete("Text");
        return document;
    }

    private static DocumentFile File(DocumentFileKind kind) => new(kind, "scan.pdf", "application/pdf", 10, Sha);
}
