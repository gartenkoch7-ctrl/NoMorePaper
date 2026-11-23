using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Application.Classification;
using NoMorePaper.Application.Documents;
using NoMorePaper.Domain.Classification;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Classification;
using NoMorePaper.Infrastructure.Documents;
using NoMorePaper.Infrastructure.Persistence;
using NoMorePaper.Infrastructure.Recognition;
using NoMorePaper.Infrastructure.Search;
using NoMorePaper.Infrastructure.Tests.Classification;
using NoMorePaper.Infrastructure.Tests.Persistence;
using NoMorePaper.Infrastructure.Tests.Processing;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Trash;

/// <summary>
/// A document in the trash is invisible in every query of the archive (ADR-0025): list, counts, detail,
/// exact and content duplicates, search, list entries, recognition examples and "to review".
/// </summary>
[Collection(SharedPostgres.Name)]
public sealed class TrashFilterQueryTests(PostgresFixture postgres)
{
    private const string Letter =
        "Stadtwerke München, Abschlagsrechnung Strom für November 2026. Kundennummer 4711. "
        + "Der Abschlag von 84,20 EUR wird am 15.11.2026 von Ihrem Konto abgebucht. Vielen Dank.";

    private static readonly Guid s_anna = Guid.CreateVersion7();
    private static readonly Guid s_bob = Guid.CreateVersion7();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ListCountsDetailAndHashes_LeaveItOut()
    {
        await using var db = await DatabaseAsync("trash_list");
        var type = await Seed.EntryAsync(db, ClassificationKind.DocumentType, "Rechnung", s_anna);
        var kept = await Seed.DocumentAsync(db, s_anna, isPrivate: false, Seed.Fields(documentTypeId: type), text: "bleibt");
        var trashed = await Seed.DocumentAsync(db, s_anna, isPrivate: false, Seed.Fields(documentTypeId: type), text: "weg", sha256: new string('a', 64));
        await Trashed.MoveAsync(db, trashed);
        var repository = new EfDocumentRepository(db);

        foreach (var viewer in new[] { s_anna, s_bob })
        {
            Assert.Equal([kept.Id], (await repository.ListAsync(viewer, 1, 50, DocumentFilter.None, Ct)).Select(d => d.Id));
            var counts = await new EfDocumentCounts(db).CountAsync(viewer, DocumentFilter.None, Ct);
            Assert.Equal((1, 1), (counts.Total, Assert.Single(counts.ByType).Count));
            Assert.Null(await repository.GetAsync(trashed.Id, viewer, Ct));
            Assert.Null(await repository.FindVisibleBySha256Async(viewer, new string('a', 64), Ct));
        }

        Assert.Null(await repository.FindOwnedBySha256Async(s_anna, new string('a', 64), Ct));
        Assert.True(await repository.ExistsAsync(trashed.Id, Ct));
    }

    [Fact]
    public async Task Search_LeavesItOut_InBothModes()
    {
        await using var db = await DatabaseAsync("trash_search");
        var kept = await Seed.DocumentAsync(db, s_anna, isPrivate: false, text: "Der Quokka auf der Wiese beim Stall.");
        var trashed = await Seed.DocumentAsync(db, s_anna, isPrivate: false, text: "Der Quokka im Stall.");
        await Trashed.MoveAsync(db, trashed);
        var index = new PostgresSearchIndex(db, NullLogger<PostgresSearchIndex>.Instance);

        Assert.Equal([kept.Id], (await index.SearchAsync("Quokka", s_anna, 10, DocumentFilter.None, Ct)).Select(hit => hit.Id));

        // The similarity fallback: the kept document with the similar word is found, the trashed one is not.
        Assert.Equal([kept.Id], (await index.SearchAsync("Stalll", s_anna, 10, DocumentFilter.None, Ct)).Select(hit => hit.Id));
    }

    [Fact]
    public async Task ContentDuplicates_IgnoreIt_OnBothSides()
    {
        await using var db = await DatabaseAsync("trash_duplicates");
        var trashed = await Seed.DocumentAsync(db, s_anna, isPrivate: false, text: Letter);
        var scan = await Seed.DocumentAsync(db, s_anna, isPrivate: false, text: Letter);
        var finder = new PostgresDuplicateFinder(db);
        Assert.Equal(trashed.Id, await finder.FindSimilarAsync(scan.Id, 0.9, Ct));

        await Trashed.MoveAsync(db, trashed);

        Assert.Null(await finder.FindSimilarAsync(scan.Id, 0.9, Ct));
        Assert.Null(await finder.FindSimilarAsync(trashed.Id, 0.9, Ct));
    }

    [Fact]
    public async Task ListEntries_UsedOnlyInTheTrash_AreNoLongerSharedByIt()
    {
        await using var db = await DatabaseAsync("trash_entries");
        var sender = await Seed.EntryAsync(db, ClassificationKind.Correspondent, "Finanzamt", s_anna);
        var tag = await Seed.EntryAsync(db, ClassificationKind.Tag, "Steuer", s_anna);
        var trashed = await Seed.DocumentAsync(db, s_anna, isPrivate: false, Seed.Fields(sender, null, null, null, tag));
        var store = new ClassificationStore(db, TimeProvider.System);
        Assert.Single(await store.VisibleEntriesAsync(s_bob, ClassificationKind.Correspondent, EntryAudience.Household, Ct));

        await Trashed.MoveAsync(db, trashed);

        Assert.Empty(await store.VisibleEntriesAsync(s_bob, ClassificationKind.Correspondent, EntryAudience.Account, Ct));
        Assert.Empty(await store.VisibleEntriesAsync(s_bob, ClassificationKind.Tag, EntryAudience.Account, Ct));
        Assert.Empty(await store.VisibleEntriesAsync(s_anna, ClassificationKind.Tag, EntryAudience.Household, Ct));
        Assert.Single(await store.VisibleEntriesAsync(s_anna, ClassificationKind.Tag, EntryAudience.Account, Ct));
    }

    [Fact]
    public async Task RecognitionExamples_AndToReview_LeaveItOut()
    {
        await using var db = await DatabaseAsync("trash_examples");
        var sender = await Seed.EntryAsync(db, ClassificationKind.Correspondent, "Stadtwerke", s_anna);
        var example = await Seed.DocumentAsync(db, s_anna, isPrivate: false, Seed.Fields(sender), text: "Abschlag");
        example.Confirm(s_anna);
        var automatic = await Seed.DocumentAsync(db, s_anna, isPrivate: false, Seed.Fields(sender), text: "Rechnung");
        var examples = new EfRecognitionExamples(db);
        Assert.Single(await examples.FindAsync(s_anna, EntryAudience.Account, [sender], Guid.CreateVersion7(), 5, Ct));

        await Trashed.MoveAsync(db, example);
        await Trashed.MoveAsync(db, automatic);

        Assert.Empty(await examples.FindAsync(s_anna, EntryAudience.Account, [sender], Guid.CreateVersion7(), 5, Ct));
        await using var read = postgres.CreateContext("trash_examples");
        var fields = new EfDocumentFieldsStore(read);
        Assert.Empty(await fields.ListReviewAsync(s_anna, Ct));
        Assert.Equal(0, await fields.CountReviewAsync(s_anna, Ct));
        Assert.Null(await fields.GetFieldsAsync(automatic.Id, s_anna, Ct));
        Assert.Null(await fields.FindVisibleAsync(automatic.Id, s_anna, Ct));
    }

    private async Task<AppDbContext> DatabaseAsync(string name) => (await QueueDatabase.CreateAsync(postgres, name)).Context();
}
