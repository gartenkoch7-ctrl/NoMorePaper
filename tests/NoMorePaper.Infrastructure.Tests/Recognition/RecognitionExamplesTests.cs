using NoMorePaper.Application.Classification;
using NoMorePaper.Domain.Classification;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Persistence;
using NoMorePaper.Infrastructure.Recognition;
using NoMorePaper.Infrastructure.Tests.Classification;
using NoMorePaper.Infrastructure.Tests.Persistence;
using NoMorePaper.Infrastructure.Tests.Processing;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Recognition;

[Collection(SharedPostgres.Name)]
public sealed class RecognitionExamplesTests(PostgresFixture postgres)
{
    private static readonly Guid s_owner = Guid.CreateVersion7();
    private static readonly Guid s_other = Guid.CreateVersion7();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Find_ReturnsConfirmedDocumentsTheOwnerMaySee_WithNamesButWithoutText()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "examples_visible");
        await using var db = data.Context();
        var correspondent = await Seed.EntryAsync(db, ClassificationKind.Correspondent, "Stadtwerke München", s_owner);
        var type = await Seed.EntryAsync(db, ClassificationKind.DocumentType, "Rechnung", s_owner);
        var power = await Seed.EntryAsync(db, ClassificationKind.Tag, "Strom", s_owner);
        var energy = await Seed.EntryAsync(db, ClassificationKind.Tag, "Energie", s_owner);
        var own = await ConfirmedAsync(db, s_owner, isPrivate: true, Seed.Fields(correspondent, type, null, null, power, energy));
        var shared = await ConfirmedAsync(db, s_other, isPrivate: false, Seed.Fields(correspondent));
        await ConfirmedAsync(db, s_other, isPrivate: true, Seed.Fields(correspondent));
        await Seed.DocumentAsync(db, s_owner, isPrivate: false, Seed.Fields(correspondent), text: "nur automatisch");

        var examples = await new EfRecognitionExamples(db).FindAsync(s_owner, EntryAudience.Account, [correspondent], Guid.CreateVersion7(), 3, Ct);

        Assert.Equal(2, examples.Count);
        Assert.Equal(("Stadtwerke München", (string?)null, "Brief"), (examples[0].Correspondent, examples[0].DocumentType, examples[0].Title));
        Assert.Empty(examples[0].Tags);
        Assert.Equal("Rechnung", examples[1].DocumentType);
        Assert.Equal(["Energie", "Strom"], examples[1].Tags);
        Assert.True(shared.ReceivedAt > own.ReceivedAt);
    }

    [Fact]
    public async Task Find_TakesTheFirstCorrespondentFirst_LeavesOutTheDocumentItself_AndStopsAtTheLimit()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "examples_order");
        await using var db = data.Context();
        var first = await Seed.EntryAsync(db, ClassificationKind.Correspondent, "Hausarztpraxis Weber", s_owner);
        var second = await Seed.EntryAsync(db, ClassificationKind.Correspondent, "AOK Bayern", s_owner);
        await ConfirmedAsync(db, s_owner, isPrivate: false, Seed.Fields(first));
        var itself = await ConfirmedAsync(db, s_owner, isPrivate: false, Seed.Fields(first));
        await ConfirmedAsync(db, s_owner, isPrivate: false, Seed.Fields(second));
        await ConfirmedAsync(db, s_owner, isPrivate: false, Seed.Fields(second));
        var examples = new EfRecognitionExamples(db);

        var two = await examples.FindAsync(s_owner, EntryAudience.Account, [first, second], itself.Id, 2, Ct);
        var none = await examples.FindAsync(s_owner, EntryAudience.Account, [], itself.Id, 3, Ct);

        Assert.Equal(["Hausarztpraxis Weber", "AOK Bayern"], two.Select(example => example.Correspondent).ToArray());
        Assert.Empty(none);
    }

    [Fact]
    public async Task Find_ForTheHousehold_LeavesOutTheOwnersPrivateDocuments()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "examples_household");
        await using var db = data.Context();
        var correspondent = await Seed.EntryAsync(db, ClassificationKind.Correspondent, "Praxis Weber", s_owner);
        var therapy = await Seed.EntryAsync(db, ClassificationKind.DocumentType, "Psychotherapie", s_owner);
        await ConfirmedAsync(db, s_owner, isPrivate: true, Seed.Fields(correspondent, therapy));
        await ConfirmedAsync(db, s_owner, isPrivate: false, Seed.Fields(correspondent));
        var examples = new EfRecognitionExamples(db);

        var forOwner = await examples.FindAsync(s_owner, EntryAudience.Account, [correspondent], Guid.CreateVersion7(), 3, Ct);
        var forHousehold = await examples.FindAsync(s_owner, EntryAudience.Household, [correspondent], Guid.CreateVersion7(), 3, Ct);

        Assert.Equal(2, forOwner.Count);
        Assert.Null(Assert.Single(forHousehold).DocumentType);
    }

    private static async Task<Document> ConfirmedAsync(AppDbContext db, Guid owner, bool isPrivate, DocumentFields fields)
    {
        var document = await Seed.DocumentAsync(db, owner, isPrivate, fields, text: "Text, der nie ein Beispiel wird.");
        document.Confirm(owner);
        await db.SaveChangesAsync();
        return document;
    }
}
