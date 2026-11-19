using NoMorePaper.Domain.Classification;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Persistence;
using NoMorePaper.Worker.Tests.Persistence;
using Xunit;

namespace NoMorePaper.Worker.Tests.Processing;

/// <summary>
/// Recognition, duplicates and the backlog see only what the document's owner may see (M3 plan,
/// "Datenschutz"): another account's private documents never show up in lists, examples or hints.
/// </summary>
[Collection(SharedPostgres.Name)]
public sealed class RecognitionVisibilityTests(PostgresFixture postgres)
{
    private const string LetterWithDoctor = RecognitionStageTests.Letter + "\nKopie an Praxis Weber, Hausarzt.";

    private static readonly Guid s_other = Guid.CreateVersion7();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Recognition_UsesListsAndExamples_OnlyFromDocumentsTheOwnerMaySee()
    {
        await using var harness = await ProcessorHarness.CreateAsync(postgres, "recognition_visibility");
        var doctor = await harness.AddDoneDocumentAsync(s_other, isPrivate: true, "Befund", ConfirmedWith("Praxis Weber", "Arztbrief"));
        var power = await harness.AddDoneDocumentAsync(s_other, isPrivate: false, "Abschlag", ConfirmedWith("Stadtwerke München", "Rechnung"));
        var id = await harness.AddQueuedDocumentAsync();
        harness.Ocr.Behavior = FakeOcrEngine.Recognizes(LetterWithDoctor);
        harness.Recognizer.Answer = RecognitionStageTests.Answer;

        await harness.ProcessAllAsync();

        var request = Assert.Single(harness.Recognizer.Requests);
        Assert.Equal(["Stadtwerke München"], request.Correspondents);
        Assert.Equal(["Rechnung"], request.DocumentTypes);
        var example = Assert.Single(request.Examples);
        Assert.Equal(("Stadtwerke München", "Rechnung", "Brief"), (example.Correspondent, example.DocumentType, example.Title));

        // The shared entry is reused, not created again for the owner.
        var document = await harness.DocumentAsync(id);
        Assert.Equal(power.CorrespondentId, document.CorrespondentId);
        Assert.Equal(power.DocumentTypeId, document.DocumentTypeId);
        Assert.NotEqual(doctor.CorrespondentId, document.CorrespondentId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Ocr_MarksAPossibleDuplicate_OnlyOfADocumentTheOwnerMaySee(bool otherIsPrivate)
    {
        await using var harness = await ProcessorHarness.CreateAsync(postgres, $"recognition_duplicate_{otherIsPrivate}".ToLowerInvariant());
        var original = await harness.AddDoneDocumentAsync(s_other, otherIsPrivate, RecognitionStageTests.Letter);
        var id = await harness.AddQueuedDocumentAsync();
        harness.Ocr.Behavior = FakeOcrEngine.Recognizes(RecognitionStageTests.Letter);

        Assert.True(await harness.Processor.ProcessNextAsync(Ct));

        Assert.Equal(otherIsPrivate ? null : original.Id, (await harness.DocumentAsync(id)).PossibleDuplicateOf);
        Assert.Equal(otherIsPrivate ? 0 : 1, harness.DuplicateLog.EventIds.Count(eventId => eventId == 5207));
    }

    [Fact]
    public async Task Backlog_IsRecognized_AfterNewUploads()
    {
        await using var harness = await ProcessorHarness.CreateAsync(postgres, "recognition_backlog");
        var old = await harness.AddDoneDocumentAsync(ProcessorHarness.Owner, isPrivate: true, "Alter Brief vom Amt");
        Assert.Equal(1, await harness.EnqueueBacklogAsync());
        var upload = await harness.AddQueuedDocumentAsync();

        Assert.True(await harness.Processor.ProcessNextAsync(Ct));
        Assert.Equal(1, harness.Ocr.Calls);
        Assert.Empty(harness.Recognizer.Requests);

        Assert.Equal(2, await harness.ProcessAllAsync());
        Assert.Equal([FakeOcrEngine.Text, "Alter Brief vom Amt"], harness.Recognizer.Requests.Select(request => request.Text).ToArray());
        Assert.Equal(RecognitionState.Automatic, (await harness.DocumentAsync(old.Id)).Recognition);
        Assert.Equal(RecognitionState.Automatic, (await harness.DocumentAsync(upload)).Recognition);
        Assert.Equal(0, await harness.EnqueueBacklogAsync());
    }

    // Fields with entries created by the other account, confirmed by it.
    private static Func<AppDbContext, Document, Task> ConfirmedWith(string correspondent, string documentType) =>
        async (db, document) =>
        {
            var sender = Correspondent.Create(correspondent, s_other, ProcessorHarness.Start);
            var type = DocumentType.Create(documentType, s_other, ProcessorHarness.Start);
            db.AddRange(sender, type);
            await db.SaveChangesAsync(Ct);
            document.Classify(new DocumentFields(null, sender.Id, type.Id, null, null, "Kurz.", []));
            document.Confirm(s_other);
        };
}
