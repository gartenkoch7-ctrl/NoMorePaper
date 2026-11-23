using NoMorePaper.Domain.Documents;
using Xunit;

namespace NoMorePaper.Domain.Tests.Documents;

/// <summary>The trash (ADR-0025): only the owner moves a document there, and it is then hidden and locked.</summary>
public sealed class DocumentTrashTests
{
    private static readonly DateTimeOffset s_at = new(2026, 11, 20, 9, 0, 0, TimeSpan.FromHours(1));
    private static readonly Guid s_owner = Guid.CreateVersion7();
    private static readonly Guid s_editor = Guid.CreateVersion7();

    [Fact]
    public void MoveToTrash_ByTheOwner_HidesTheDocument_FromEverybody()
    {
        var document = Shared();

        document.MoveToTrash(s_owner, s_at);

        Assert.True(document.IsInTrash);
        Assert.Equal(s_at, document.DeletedAt);
        Assert.Equal(TimeSpan.Zero, document.DeletedAt?.Offset);
        Assert.Equal(s_owner, document.DeletedBy);
        Assert.False(document.IsVisibleTo(s_owner));
        Assert.False(document.IsVisibleTo(s_editor));
        Assert.False(document.CanEdit(s_owner));
        Assert.False(document.CanEdit(s_editor));
    }

    [Fact]
    public void MoveToTrash_IsRefused_ForAnEditor_AndTwice()
    {
        var document = Shared();
        Assert.Throws<InvalidOperationException>(() => document.MoveToTrash(s_editor, s_at));
        Assert.False(document.IsInTrash);

        document.MoveToTrash(s_owner, s_at);
        Assert.Throws<InvalidOperationException>(() => document.MoveToTrash(s_owner, s_at));
    }

    [Fact]
    public void RestoreFromTrash_ByTheOwner_ShowsItAgain_WithItsEditors()
    {
        var document = Shared();
        document.MoveToTrash(s_owner, s_at);
        Assert.Throws<InvalidOperationException>(() => document.RestoreFromTrash(s_editor));

        document.RestoreFromTrash(s_owner);

        Assert.False(document.IsInTrash);
        Assert.Null(document.DeletedAt);
        Assert.Null(document.DeletedBy);
        Assert.True(document.CanEdit(s_editor));
        Assert.Throws<InvalidOperationException>(() => document.RestoreFromTrash(s_owner));
    }

    private static Document Shared()
    {
        var document = Document.Receive("Brief", s_at, s_owner, isPrivate: false);
        document.SetEditors(s_owner, [s_editor]);
        return document;
    }
}
