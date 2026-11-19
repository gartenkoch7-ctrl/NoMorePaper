using NoMorePaper.Domain.Documents;
using Xunit;

namespace NoMorePaper.Domain.Tests.Documents;

public sealed class DocumentFieldsTests
{
    private static readonly Guid s_tag = Guid.CreateVersion7();

    [Fact]
    public void Fields_KeepTheirValues_AndDropDuplicateTags()
    {
        var correspondent = Guid.CreateVersion7();
        var type = Guid.CreateVersion7();
        var other = Guid.CreateVersion7();

        var fields = new DocumentFields("Stromrechnung", correspondent, type, new DateOnly(2026, 11, 1), new Money(84.20m, "EUR"), "Abschlag.", [s_tag, other, s_tag]);

        Assert.Equal(("Stromrechnung", correspondent, type), (fields.Title, fields.CorrespondentId, fields.DocumentTypeId));
        Assert.Equal((new DateOnly(2026, 11, 1), new Money(84.20m, "EUR"), "Abschlag."), (fields.DocumentDate, fields.Amount, fields.Summary));
        Assert.Equal([s_tag, other], fields.TagIds);
    }

    [Fact]
    public void Fields_MayAllBeEmpty()
    {
        var fields = new DocumentFields(null, null, null, null, null, null, []);

        Assert.Null(fields.Title);
        Assert.Empty(fields.TagIds);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Fields_RejectAnEmptyTitle(string title) =>
        Assert.ThrowsAny<ArgumentException>(() => new DocumentFields(title, null, null, null, null, null, []));

    [Fact]
    public void Fields_RejectATooLongTitle() =>
        Assert.Throws<ArgumentException>(() =>
            new DocumentFields(new string('a', Document.MaxTitleLength + 1), null, null, null, null, null, []));

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Fields_RejectAnEmptySummary(string summary) =>
        Assert.ThrowsAny<ArgumentException>(() => new DocumentFields(null, null, null, null, null, summary, []));

    [Fact]
    public void Fields_RejectATooLongSummary()
    {
        _ = new DocumentFields(null, null, null, null, null, new string('a', DocumentFields.MaxSummaryLength), []);

        Assert.ThrowsAny<ArgumentException>(() =>
            new DocumentFields(null, null, null, null, null, new string('a', DocumentFields.MaxSummaryLength + 1), []));
    }

    [Fact]
    public void Fields_RejectEmptyIds()
    {
        Assert.Throws<ArgumentException>(() => new DocumentFields(null, Guid.Empty, null, null, null, null, []));
        Assert.Throws<ArgumentException>(() => new DocumentFields(null, null, Guid.Empty, null, null, null, []));
        Assert.Throws<ArgumentException>(() => new DocumentFields(null, null, null, null, null, null, [s_tag, Guid.Empty]));
    }
}
