using NoMorePaper.Domain.Documents;
using NoMorePaper.Domain.Editing;
using Xunit;

namespace NoMorePaper.Domain.Tests.Documents;

/// <summary>Choice of versions and split suggestion of a document (ADR-0025).</summary>
public sealed class DocumentEditingTests
{
    private static readonly Guid s_owner = Guid.CreateVersion7();

    [Fact]
    public void ChooseVersions_OnlyByTheOwner_AndOnlyKnownChoices()
    {
        var document = Document.Receive("Brief", DateTimeOffset.UnixEpoch, s_owner, isPrivate: false);
        var editor = Guid.CreateVersion7();
        document.SetEditors(s_owner, [editor]);
        Assert.Null(document.VersionPolicy);

        document.ChooseVersions(s_owner, VersionPolicy.EditedOnly);

        Assert.Equal(VersionPolicy.EditedOnly, document.VersionPolicy);
        Assert.Throws<InvalidOperationException>(() => document.ChooseVersions(editor, VersionPolicy.KeepBoth));
        Assert.Throws<ArgumentOutOfRangeException>(() => document.ChooseVersions(s_owner, (VersionPolicy)7));
        Assert.Equal(VersionPolicy.EditedOnly, document.VersionPolicy);
    }

    [Fact]
    public void SuggestSplit_ReplacesTheSuggestion_AndDismissDropsIt()
    {
        var document = Document.Receive("Stapel", DateTimeOffset.UnixEpoch, s_owner, isPrivate: false);
        var first = new SplitSuggestion([new PageRange(1, 2), new PageRange(3, 3)], [], [SplitReason.Salutation]);
        var second = new SplitSuggestion([new PageRange(1, 1), new PageRange(3, 3)], [2], [SplitReason.Blank]);

        document.SuggestSplit(first);
        document.SuggestSplit(second);
        Assert.Same(second, document.SplitSuggestion);

        document.DismissSplit();
        Assert.Null(document.SplitSuggestion);
    }
}
