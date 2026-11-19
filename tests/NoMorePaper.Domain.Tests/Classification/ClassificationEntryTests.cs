using NoMorePaper.Domain.Classification;
using Xunit;

namespace NoMorePaper.Domain.Tests.Classification;

public sealed class ClassificationEntryTests
{
    private static readonly DateTimeOffset s_createdAt = new(2026, 11, 19, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid s_creator = Guid.CreateVersion7();

    [Fact]
    public void Create_KeepsTheTrimmedName_ItsNormalizedForm_AndTheCreator()
    {
        var entry = Correspondent.Create(" Stadtwerke München GmbH ", s_creator, s_createdAt);

        Assert.Equal(("Stadtwerke München GmbH", "stadtwerke munchen", s_creator), (entry.Name, entry.NormalizedName, entry.CreatedBy));
    }

    [Fact]
    public void Create_DerivesAVersion7IdFromTheGivenTime()
    {
        var entry = DocumentType.Create("Rechnung", s_creator, s_createdAt.ToOffset(TimeSpan.FromHours(1)));

        Assert.Equal(7, entry.Id.Version);
        var bytes = entry.Id.ToByteArray(bigEndian: true);
        var millis = ((long)bytes[0] << 40) | ((long)bytes[1] << 32) | ((long)bytes[2] << 24) | ((long)bytes[3] << 16) | ((long)bytes[4] << 8) | bytes[5];
        Assert.Equal(s_createdAt.ToUnixTimeMilliseconds(), millis);
    }

    [Fact]
    public void Create_RequiresACreator() =>
        Assert.Throws<ArgumentException>(() => Tag.Create("Strom", Guid.Empty, s_createdAt));

    [Theory]
    [InlineData("")]
    [InlineData("...")]
    public void Create_RejectsInvalidNames(string name) =>
        Assert.ThrowsAny<ArgumentException>(() => Tag.Create(name, s_creator, s_createdAt));

    [Fact]
    public void Rename_ChangesNameAndNormalizedName()
    {
        var entry = Tag.Create("Strom", s_creator, s_createdAt);

        entry.Rename(" Gas ");

        Assert.Equal(("Gas", "gas"), (entry.Name, entry.NormalizedName));
    }

    [Fact]
    public void Rename_RejectsAnInvalidName_AndKeepsTheOldOne()
    {
        var entry = Tag.Create("Strom", s_creator, s_createdAt);

        Assert.ThrowsAny<ArgumentException>(() => entry.Rename(" "));
        Assert.Equal(("Strom", "strom"), (entry.Name, entry.NormalizedName));
    }
}
