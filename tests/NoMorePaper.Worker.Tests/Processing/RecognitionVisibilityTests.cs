using Microsoft.EntityFrameworkCore;
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

    [Fact]
    public async Task Recognition_OfASharedDocument_NeverUsesTheOwnersPrivateOnlyEntriesOrExamples()
    {
        await using var harness = await ProcessorHarness.CreateAsync(postgres, "recognition_household");
        var owner = ProcessorHarness.Owner;
        var therapy = await harness.AddDoneDocumentAsync(owner, isPrivate: true, "Befund", ConfirmedWith("Stadtwerke München", "Rechnung", owner));
        var id = await harness.AddQueuedDocumentAsync();
        harness.Ocr.Behavior = FakeOcrEngine.Recognizes(RecognitionStageTests.Letter);
        harness.Recognizer.Answer = RecognitionStageTests.Answer;

        await harness.ProcessAllAsync();

        var request = Assert.Single(harness.Recognizer.Requests);
        Assert.Equal((0, 0, 0), (request.Correspondents.Count, request.DocumentTypes.Count, request.Examples.Count));

        // New entries of the same names; the private ones stay used by the private document only.
        var document = await harness.DocumentAsync(id);
        Assert.NotNull(document.CorrespondentId);
        Assert.NotEqual(therapy.CorrespondentId, document.CorrespondentId);
        Assert.NotNull(document.DocumentTypeId);
        Assert.NotEqual(therapy.DocumentTypeId, document.DocumentTypeId);
    }

    [Fact]
    public async Task Recognition_RunsAgainWithTheHouseholdsLists_WhenTheDocumentIsSharedMeanwhile()
    {
        await using var harness = await ProcessorHarness.CreateAsync(postgres, "recognition_shared_meanwhile");
        var owner = ProcessorHarness.Owner;
        var therapy = await harness.AddDoneDocumentAsync(owner, isPrivate: true, "Befund", ConfirmedWith("Stadtwerke München", "Rechnung", owner));
        var id = await harness.AddQueuedDocumentAsync(isPrivate: true);
        harness.Ocr.Behavior = FakeOcrEngine.Recognizes(RecognitionStageTests.Letter);
        harness.Recognizer.Behavior = async (_, _) =>
        {
            if (harness.Recognizer.Requests.Count == 1)
            {
                await using var db = harness.OtherWorkerContext();
                var shared = await db.Documents.SingleAsync(found => found.Id == id, Ct);
                shared.Share(owner);
                await db.SaveChangesAsync(Ct);
            }

            return RecognitionStageTests.Answer;
        };

        await harness.ProcessAllAsync();

        Assert.Equal(2, harness.Recognizer.Requests.Count);
        Assert.Equal(["Stadtwerke München"], harness.Recognizer.Requests[0].Correspondents);
        Assert.Empty(harness.Recognizer.Requests[1].Correspondents);
        Assert.Single(harness.Recognizer.Requests[0].Examples);
        Assert.Empty(harness.Recognizer.Requests[1].Examples);
        Assert.Contains(5212, harness.RecognitionLog.EventIds);
        var document = await harness.DocumentAsync(id);
        Assert.Equal(RecognitionState.Automatic, document.Recognition);
        Assert.NotEqual(therapy.CorrespondentId, document.CorrespondentId);
        Assert.NotEqual(therapy.DocumentTypeId, document.DocumentTypeId);
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

    // Fields with entries created by the account (the other one by default), confirmed by it.
    private static Func<AppDbContext, Document, Task> ConfirmedWith(string correspondent, string documentType, Guid? account = null) =>
        async (db, document) =>
        {
            var by = account ?? s_other;
            var sender = Correspondent.Create(correspondent, by, ProcessorHarness.Start);
            var type = DocumentType.Create(documentType, by, ProcessorHarness.Start);
            db.AddRange(sender, type);
            await db.SaveChangesAsync(Ct);
            document.Classify(new DocumentFields(null, sender.Id, type.Id, null, null, "Kurz.", []));
            document.Confirm(by);
        };
}
