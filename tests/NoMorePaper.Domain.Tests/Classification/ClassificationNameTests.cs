using NoMorePaper.Domain.Classification;
using Xunit;

namespace NoMorePaper.Domain.Tests.Classification;

public sealed class ClassificationNameTests
{
    [Theory]
    [InlineData("Stadtwerke München GmbH", "stadtwerke munchen")]
    [InlineData("stadtwerke-munchen", "stadtwerke munchen")]
    [InlineData("  STADTWERKE   MÜNCHEN  ", "stadtwerke munchen")]
    [InlineData("E.ON SE", "eon")]
    [InlineData("EON", "eon")]
    [InlineData("Müller GmbH & Co. KG", "muller")]
    [InlineData("Bäckerei Schmidt UG (haftungsbeschränkt)", "backerei schmidt")]
    [InlineData("Sportverein Musterstadt e.V.", "sportverein musterstadt")]
    [InlineData("Volksbank eG", "volksbank")]
    [InlineData("Straße", "strasse")]
    [InlineData("Café Crème", "cafe creme")]
    [InlineData("ACME Ltd.", "acme")]
    [InlineData("AG Leipzig", "ag leipzig")]
    [InlineData("AG", "ag")]
    [InlineData("1&1", "11")]
    [InlineData("Rechnung/Gutschrift", "rechnung gutschrift")]
    public void Normalize_IgnoresCaseAccentsPunctuationAndTrailingLegalForms(string name, string expected) =>
        Assert.Equal(expected, ClassificationName.Normalize(name));

    [Fact]
    public void Normalize_OfNothingButPunctuation_IsEmpty() =>
        Assert.Equal(string.Empty, ClassificationName.Normalize("!?."));

    [Fact]
    public void Clean_Trims() =>
        Assert.Equal("Stadtwerke", ClassificationName.Clean("  Stadtwerke ", "name"));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("--")]
    [InlineData("!?.")]
    public void Clean_RejectsNamesWithoutLettersOrDigits(string name) =>
        Assert.ThrowsAny<ArgumentException>(() => ClassificationName.Clean(name, "name"));

    [Fact]
    public void Clean_RejectsTooLongNames()
    {
        ClassificationName.Clean(new string('a', ClassificationName.MaxLength), "name");

        Assert.Throws<ArgumentException>(() => ClassificationName.Clean(new string('a', ClassificationName.MaxLength + 1), "name"));
    }

    [Fact]
    public void Clean_MeasuresTheTrimmedName() =>
        Assert.Equal(ClassificationName.MaxLength, ClassificationName.Clean(" " + new string('a', ClassificationName.MaxLength) + " ", "name").Length);
}
