using NoMorePaper.Domain.Documents;
using NoMorePaper.Domain.Editing;
using Xunit;

namespace NoMorePaper.Domain.Tests.Editing;

/// <summary>Appending a document to the open one (ADR-0025): who may, and when the result is private.</summary>
public sealed class AppendRuleTests
{
    private static readonly Guid s_owner = Guid.CreateVersion7();

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public void ResultIsPrivate_WhenEitherIs(bool targetPrivate, bool appendedPrivate, bool expected) =>
        Assert.Equal(expected, AppendRule.ResultIsPrivate(Done(targetPrivate), Done(appendedPrivate)));

    [Fact]
    public void Check_AllowsTwoDoneDocumentsTheAccountMayEdit() =>
        Assert.Null(AppendRule.Check(Done(), Done(), s_owner));

    [Fact]
    public void Check_NamesTheFirstReason()
    {
        var target = Done();
        var trashed = Done();
        trashed.MoveToTrash(s_owner, DateTimeOffset.UnixEpoch);
        var queued = Document.Receive("Neu", DateTimeOffset.UnixEpoch, s_owner, isPrivate: false);
        var foreign = Done(owner: Guid.CreateVersion7());

        Assert.Equal(AppendRefusal.Same, AppendRule.Check(target, target, s_owner));
        Assert.Equal(AppendRefusal.InTrash, AppendRule.Check(target, trashed, s_owner));
        Assert.Equal(AppendRefusal.InTrash, AppendRule.Check(trashed, target, s_owner));
        Assert.Equal(AppendRefusal.NotEditable, AppendRule.Check(target, foreign, s_owner));
        Assert.Equal(AppendRefusal.NotDone, AppendRule.Check(target, queued, s_owner));
    }

    private static Document Done(bool isPrivate = false, Guid? owner = null)
    {
        var document = Document.Receive("Brief", DateTimeOffset.UnixEpoch, owner ?? s_owner, isPrivate);
        document.StartProcessing();
        document.Complete("Text");
        return document;
    }
}
