using NoMorePaper.Domain.Documents;
using Xunit;

namespace NoMorePaper.Domain.Tests.Documents;

public sealed class DocumentEditorTests
{
    private static readonly DateTimeOffset s_receivedAt = new(2026, 11, 19, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid s_owner = Guid.CreateVersion7();
    private static readonly Guid s_editor = Guid.CreateVersion7();
    private static readonly Guid s_other = Guid.CreateVersion7();

    [Fact]
    public void TheOwner_MayEdit_OthersMayNot()
    {
        var document = Receive(isPrivate: false);

        Assert.True(document.CanEdit(s_owner));
        Assert.False(document.CanEdit(s_editor));
        Assert.False(document.CanEdit(Guid.Empty));
    }

    [Fact]
    public void ChosenEditors_MayEdit_AndEditTheFields()
    {
        var document = Receive(isPrivate: false);

        document.SetEditors(s_owner, [s_editor]);
        document.Edit(s_editor, new DocumentFields("Vom Bearbeiter", null, null, null, null, null, []));

        Assert.True(document.CanEdit(s_editor));
        Assert.False(document.CanEdit(s_other));
        Assert.Equal(("Vom Bearbeiter", RecognitionState.Confirmed), (document.Title, document.Recognition));
    }

    [Fact]
    public void Editors_MayNotEditAPrivateDocument_TheyCannotSee()
    {
        var document = Receive(isPrivate: false);
        document.SetEditors(s_owner, [s_editor]);

        document.MakePrivate(s_owner);
        Assert.False(document.CanEdit(s_editor));
        Assert.True(document.CanEdit(s_owner));

        document.Share(s_owner);
        Assert.True(document.CanEdit(s_editor));
    }

    [Fact]
    public void OnlyTheOwner_ChoosesEditors()
    {
        var document = Receive(isPrivate: false);
        document.SetEditors(s_owner, [s_editor]);

        Assert.Throws<InvalidOperationException>(() => document.SetEditors(s_editor, [s_other]));
        Assert.Throws<InvalidOperationException>(() => document.SetEditors(s_other, [s_other]));
        Assert.Equal([s_editor], document.Editors.Select(editor => editor.AccountId));
    }

    [Fact]
    public void SetEditors_ReplacesTheList_WithoutTheOwnerAndDuplicates()
    {
        var document = Receive(isPrivate: false);
        document.SetEditors(s_owner, [s_editor]);
        var kept = document.Editors.Single();

        document.SetEditors(s_owner, [s_owner, s_editor, s_other, s_other]);

        Assert.Equal([s_editor, s_other], document.Editors.Select(editor => editor.AccountId));
        Assert.Same(kept, document.Editors[0]);

        document.SetEditors(s_owner, []);
        Assert.Empty(document.Editors);
    }

    [Fact]
    public void SetEditors_RejectsAnEmptyId()
    {
        var document = Receive(isPrivate: false);

        Assert.Throws<ArgumentException>(() => document.SetEditors(s_owner, [s_editor, Guid.Empty]));
        Assert.Empty(document.Editors);
    }

    private static Document Receive(bool isPrivate) => Document.Receive("scan.pdf", s_receivedAt, s_owner, isPrivate);
}
