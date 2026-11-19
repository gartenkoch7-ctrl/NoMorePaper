using NoMorePaper.Infrastructure.Recognition;
using NoMorePaper.Infrastructure.Tests.Classification;
using NoMorePaper.Infrastructure.Tests.Persistence;
using NoMorePaper.Infrastructure.Tests.Processing;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Recognition;

[Collection(SharedPostgres.Name)]
public sealed class DuplicateFinderTests(PostgresFixture postgres)
{
    private const string Letter =
        "Stadtwerke München, Abschlagsrechnung Strom für November 2026. Kundennummer 4711. "
        + "Der Abschlag von 84,20 EUR wird am 15.11.2026 von Ihrem Konto abgebucht. Vielen Dank.";

    private static readonly Guid s_owner = Guid.CreateVersion7();
    private static readonly Guid s_other = Guid.CreateVersion7();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task FindSimilar_FindsASharedDocumentOfAnotherAccount_WithTheSameText()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "duplicates_shared");
        await using var db = data.Context();
        var original = await Seed.DocumentAsync(db, s_other, isPrivate: false, text: Letter);
        var scan = await Seed.DocumentAsync(db, s_owner, isPrivate: false, text: Letter.Replace("Konto", "K0nto", StringComparison.Ordinal));

        Assert.Equal(original.Id, await new PostgresDuplicateFinder(db).FindSimilarAsync(scan.Id, 0.9, Ct));
    }

    [Fact]
    public async Task FindSimilar_IgnoresPrivateDocumentsOfOthers_ButFindsOwnPrivateOnes()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "duplicates_private");
        await using var db = data.Context();
        await Seed.DocumentAsync(db, s_other, isPrivate: true, text: Letter);
        var scan = await Seed.DocumentAsync(db, s_owner, isPrivate: false, text: Letter);
        var finder = new PostgresDuplicateFinder(db);

        Assert.Null(await finder.FindSimilarAsync(scan.Id, 0.9, Ct));

        var own = await Seed.DocumentAsync(db, s_owner, isPrivate: true, text: Letter);
        Assert.Equal(own.Id, await finder.FindSimilarAsync(scan.Id, 0.9, Ct));
    }

    [Fact]
    public async Task FindSimilar_IgnoresDifferentTexts_UnfinishedDocuments_AndItself()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "duplicates_none");
        await using var db = data.Context();
        await Seed.DocumentAsync(db, s_owner, isPrivate: false, text: "Mietvertrag für die Wohnung in der Lindenstraße 5, Beginn 1. Januar 2027.");
        await Seed.DocumentAsync(db, s_owner, isPrivate: false);
        var scan = await Seed.DocumentAsync(db, s_owner, isPrivate: false, text: Letter);

        Assert.Null(await new PostgresDuplicateFinder(db).FindSimilarAsync(scan.Id, 0.9, Ct));
    }

    [Fact]
    public async Task FindSimilar_IgnoresADocumentMarkedAsDuplicateOfThisOne()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "duplicates_one_way");
        await using var db = data.Context();
        var first = await Seed.DocumentAsync(db, s_owner, isPrivate: false, text: Letter);
        var second = await Seed.DocumentAsync(db, s_owner, isPrivate: false, text: Letter);
        var finder = new PostgresDuplicateFinder(db);
        Assert.True(await finder.MarkAsync(second.Id, first.Id, Ct));

        Assert.Null(await finder.FindSimilarAsync(first.Id, 0.9, Ct));
    }

    [Fact]
    public async Task FindSimilar_PicksTheMostSimilar_AndMarkSetsTheHint()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "duplicates_most_similar");
        await using var db = data.Context();
        await Seed.DocumentAsync(db, s_owner, isPrivate: false, text: Letter + " Ihre Stadtwerke, Kundenservice, Montag bis Freitag.");
        var exact = await Seed.DocumentAsync(db, s_owner, isPrivate: false, text: Letter);
        var scan = await Seed.DocumentAsync(db, s_owner, isPrivate: false, text: Letter);
        var finder = new PostgresDuplicateFinder(db);

        var found = await finder.FindSimilarAsync(scan.Id, 0.5, Ct);
        Assert.Equal(exact.Id, found);
        Assert.True(await finder.MarkAsync(scan.Id, found!.Value, Ct));
        Assert.False(await finder.MarkAsync(Guid.CreateVersion7(), found.Value, Ct));

        Assert.Equal(exact.Id, (await data.DocumentAsync(scan.Id)).PossibleDuplicateOf);
    }
}
