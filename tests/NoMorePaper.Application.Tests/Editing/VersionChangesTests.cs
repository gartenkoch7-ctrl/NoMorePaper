using NoMorePaper.Application.Editing;
using NoMorePaper.Application.Trash;
using NoMorePaper.Domain.Documents;
using Xunit;
using static NoMorePaper.Application.Tests.Editing.EditSamples;

namespace NoMorePaper.Application.Tests.Editing;

/// <summary>The version rule of ADR-0025 when a new version is stored.</summary>
public sealed class VersionChangesTests
{
    private static readonly Guid s_owner = Guid.CreateVersion7();
    private static readonly DocumentFile s_newEdited = File(DocumentFileKind.Edited, 'd');
    private static readonly DocumentFile s_newArchive = File(DocumentFileKind.Archive, 'e');

    [Fact]
    public void KeepBoth_KeepsTheOriginal_AndTrashesOnlyThePreviousEditedVersion()
    {
        var read = New().Done(edited: true);
        var tracked = New().Done(edited: true);

        var changes = VersionChanges.For(read, VersionPolicy.KeepBoth, s_newEdited, s_newArchive, Now);

        var item = Assert.Single(changes.Items);
        Assert.Equal((TrashItemKind.Edited, new string('c', 64)), (item.Kind, item.File!.Sha256));
        Assert.Equal([new FinishMove(FinishMoveKind.PreviousEdited, item.Id)], changes.Moves);
        Assert.True(changes.KeepsEdited);
        Assert.True(changes.ApplyTo(tracked));
        Assert.Equal((new string('a', 64), new string('d', 64), new string('e', 64)), Shas(tracked));
        Assert.Equal(Now, tracked.EditedAt);
    }

    [Fact]
    public void EditedOnly_TrashesTheOriginal()
    {
        var read = New().Done();
        var tracked = New().Done();

        var changes = VersionChanges.For(read, VersionPolicy.EditedOnly, s_newEdited, s_newArchive, Now);

        Assert.Equal([FinishMoveKind.Original], changes.Moves.Select(move => move.Kind));
        Assert.True(changes.ApplyTo(tracked));
        Assert.Equal((null, new string('d', 64), new string('e', 64)), Shas(tracked));
    }

    [Fact]
    public void OriginalOnly_TrashesTheNewVersion_AndThePreviousOne()
    {
        var read = New().Done(edited: true);
        var tracked = New().Done(edited: true);

        var changes = VersionChanges.For(read, VersionPolicy.OriginalOnly, s_newEdited, s_newArchive, Now);

        Assert.Equal([FinishMoveKind.PreviousEdited, FinishMoveKind.NewEdited], changes.Moves.Select(move => move.Kind));
        Assert.Equal(new string('d', 64), changes.Items[1].File!.Sha256);
        Assert.False(changes.KeepsEdited);
        Assert.True(changes.ApplyTo(tracked));
        Assert.Equal((new string('a', 64), null, new string('e', 64)), Shas(tracked));
    }

    [Fact]
    public void OriginalOnly_WithoutOriginal_KeepsTheEdit_BecauseADocumentNeedsASource()
    {
        var read = New().Done(edited: true);
        read.RemoveFile(DocumentFileKind.Original);

        Assert.True(VersionRule.KeepsEdited(read, VersionPolicy.OriginalOnly));
        Assert.False(VersionRule.DropsOriginal(read, VersionPolicy.EditedOnly));
        Assert.True(VersionChanges.For(read, VersionPolicy.OriginalOnly, s_newEdited, s_newArchive, Now).KeepsEdited);
    }

    [Theory]
    [InlineData(EditKind.Append, VersionPolicy.OriginalOnly, VersionPolicy.KeepBoth)]
    [InlineData(EditKind.Append, VersionPolicy.EditedOnly, VersionPolicy.EditedOnly)]
    [InlineData(EditKind.Append, VersionPolicy.KeepBoth, VersionPolicy.KeepBoth)]
    [InlineData(EditKind.Pages, VersionPolicy.OriginalOnly, VersionPolicy.OriginalOnly)]
    [InlineData(EditKind.Rebuild, VersionPolicy.OriginalOnly, VersionPolicy.OriginalOnly)]
    public void AnAppend_UnderOnlyOriginal_KeepsBoth(EditKind kind, VersionPolicy policy, VersionPolicy expected)
    {
        Assert.Equal(expected, VersionRule.For(New().Done(), kind, policy));

        // Without original the edit is kept anyway; the document keeps its policy.
        var withoutOriginal = New().Done(edited: true);
        withoutOriginal.RemoveFile(DocumentFileKind.Original);
        Assert.Equal(policy, VersionRule.For(withoutOriginal, kind, policy));
    }

    [Fact]
    public void AnAppend_UnderOnlyOriginal_KeepsTheNewEditedVersion_AndTheOriginal()
    {
        var read = New().Done();
        var tracked = New().Done();

        var changes = VersionChanges.For(read, VersionRule.For(read, EditKind.Append, VersionPolicy.OriginalOnly), s_newEdited, s_newArchive, Now);

        Assert.Empty(changes.Items);
        Assert.True(changes.KeepsEdited);
        Assert.True(changes.ApplyTo(tracked));
        Assert.Equal((new string('a', 64), new string('d', 64), new string('e', 64)), Shas(tracked));
    }

    [Fact]
    public void ARebuild_ReplacesOnlyTheArchive()
    {
        var read = New().Done(edited: true);
        var tracked = New().Done(edited: true);

        var changes = VersionChanges.For(read, VersionPolicy.OriginalOnly, null, s_newArchive, Now);

        Assert.Empty(changes.Items);
        Assert.True(changes.ApplyTo(tracked));
        Assert.Equal((new string('a', 64), new string('c', 64), new string('e', 64)), Shas(tracked));
    }

    [Fact]
    public void ChangedFiles_AreNotTouched()
    {
        var read = New().Done();
        var tracked = New().Done(edited: true);

        Assert.False(VersionChanges.For(read, VersionPolicy.KeepBoth, s_newEdited, s_newArchive, Now).ApplyTo(tracked));
        Assert.Equal((new string('a', 64), new string('c', 64), new string('b', 64)), Shas(tracked));
    }

    [Fact]
    public void TextLayers_ComeFromWhereThePagesCameFrom()
    {
        IReadOnlyList<PageInfo> source = [new(1, "a", false, true), new(2, "b", false, false)];
        var layers = PageTextLayers.Of([(2, false), (1, true), (1, false), (7, false)], source);
        Assert.Equal([false, false, true, null], layers);

        IReadOnlyList<PageInfo> analyzed = [new(1, "x", false, true), new(2, "y", false, true), new(3, "z", false, true), new(4, "w", false, true)];
        Assert.Equal([false, false, true, true], PageTextLayers.Apply(analyzed, layers).Select(page => page.HadTextLayer));
    }

    // The document as read and as tracked under the lock are two instances; the rule compares their files.
    private static Document New() => Document.Receive("Brief", Now.AddDays(-2), s_owner, isPrivate: false);

    private static (string? Original, string? Edited, string? Archive) Shas(Document document) => (
        document.FindFile(DocumentFileKind.Original)?.Sha256,
        document.FindFile(DocumentFileKind.Edited)?.Sha256,
        document.FindFile(DocumentFileKind.Archive)?.Sha256);
}
