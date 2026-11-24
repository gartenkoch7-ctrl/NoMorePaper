using NoMorePaper.Application.Editing;
using NoMorePaper.Domain.Editing;
using NoMorePaper.Worker.Tests.Persistence;
using NoMorePaper.Worker.Tests.Processing;
using Xunit;
using static NoMorePaper.Worker.Tests.Editing.EditFlow;

namespace NoMorePaper.Worker.Tests.Editing;

/// <summary>Edits and the stack separation of track D: a new version drops the suggestion; split parts are separated already.</summary>
[Collection(SharedPostgres.Name)]
public sealed class EditSeparationTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(EditKind.Pages)]
    [InlineData(EditKind.Rebuild)]
    public async Task ANewVersion_DismissesTheSplitSuggestion(EditKind kind)
    {
        await using var harness = await ProcessorHarness.CreateAsync(postgres, $"edit_separation_dismiss_{kind}".ToLowerInvariant());
        var id = await harness.DoneDocumentAsync();
        await using (var db = harness.OtherWorkerContext())
        {
            var document = await db.Documents.FindAsync([id], Ct);
            document!.SuggestSplit(new SplitSuggestion([new PageRange(1, 1), new PageRange(2, 3)], [], [SplitReason.Salutation]));
            await db.SaveChangesAsync(Ct);
        }

        await harness.PlanAsync(id, kind, data => kind == EditKind.Pages ? data with { Steps = Steps((1, PageRotation.None, false)) } : data);
        await harness.ProcessAllAsync();

        Assert.Null((await harness.DocumentAsync(id)).SplitSuggestion);
    }

    [Fact]
    public async Task SplitParts_AreMadeFromTheArchive_WhosePagesCarryText_SoTheySkipTheSeparation()
    {
        await using var harness = await ProcessorHarness.CreateAsync(postgres, "edit_separation_parts");
        var id = await harness.DoneDocumentAsync();
        EditStageTests.ArchiveOfInput(harness, "Bearbeitet");
        await harness.PlanAsync(id, EditKind.Pages, data => data with { Steps = Steps((2, PageRotation.None, false), (1, PageRotation.None, false)) });
        await harness.ProcessAllAsync();

        await harness.PlanAsync(id, EditKind.Split, data => data with
        {
            Parts = [Steps((1, PageRotation.None, false)), Steps((2, PageRotation.None, false))],
        });
        Assert.True(await harness.Processor.ProcessNextAsync(Ct));

        Assert.Equal(["archive-source.pdf"], harness.Pdf.Composed.Skip(1).SelectMany(parts => parts).Select(page => page.File).Distinct());
        Assert.All(harness.Pdf.Sources.Skip(1), source => Assert.Equal("%PDF archive of edited.pdf", source));
    }
}
