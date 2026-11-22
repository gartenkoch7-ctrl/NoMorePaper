using NoMorePaper.Application.Documents;
using Xunit;

namespace NoMorePaper.Application.Tests.Documents;

public sealed class DocumentExcerptTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \n ")]
    public void From_NothingToShow_IsNull(string? text) => Assert.Null(DocumentExcerpt.From(text));

    [Fact]
    public void From_CollapsesWhitespace() => Assert.Equal("a b", DocumentExcerpt.From("a\n\n b"));

    [Fact]
    public void From_ExactlyMaxLength_IsUnchanged()
    {
        var text = new string('a', DocumentExcerpt.MaxLength);

        Assert.Equal(text, DocumentExcerpt.From(text));
    }

    [Fact]
    public void From_LongText_CutsAtTheLastSpaceBeforeTheLimit()
    {
        var text = new string('a', 150) + " " + new string('b', 49);

        var excerpt = DocumentExcerpt.From(text);

        Assert.Equal(new string('a', 150) + "…", excerpt);
    }

    [Fact]
    public void From_LongTextWithoutSpaces_CutsHard()
    {
        var excerpt = DocumentExcerpt.From(new string('a', 200));

        Assert.Equal(new string('a', 160) + "…", excerpt);
    }

    [Fact]
    public void From_OnlyAnEarlySpace_CutsHard()
    {
        var text = new string('a', 40) + " " + new string('b', 159);

        var excerpt = DocumentExcerpt.From(text);

        Assert.Equal(text[..160] + "…", excerpt);
    }
}
