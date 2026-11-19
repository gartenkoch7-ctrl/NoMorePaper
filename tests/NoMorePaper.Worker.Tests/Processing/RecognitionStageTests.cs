using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Processing;
using NoMorePaper.Application.Recognition;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Worker.Tests.Persistence;
using Xunit;

namespace NoMorePaper.Worker.Tests.Processing;

/// <summary>The recognition stage after OCR, with a fake LLM on a real database.</summary>
[Collection(SharedPostgres.Name)]
public sealed class RecognitionStageTests(PostgresFixture postgres)
{
    public const string Letter = """
        Stadtwerke München GmbH, Emmy-Noether-Str. 2, 80992 München
        Abschlagsrechnung Strom November 2026, München, 01.11.2026
        Der Abschlag von 84,20 EUR wird am 15.11.2026 von Ihrem Konto abgebucht.
        """;

    public static readonly RecognitionResult Answer = new(
        "Stadtwerke München", "Rechnung", new DateOnly(2026, 11, 1), new Money(84.2m, "EUR"), ["Strom"],
        "Strom November 2026", "Abschlag von 84,20 EUR für November.");

    private static readonly DateTimeOffset s_start = ProcessorHarness.Start;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Upload_IsRecognizedAfterOcr_WithNewEntriesOfItsOwner()
    {
        await using var harness = await ProcessorHarness.CreateAsync(postgres, "recognition_upload");
        var id = await harness.AddQueuedDocumentAsync();
        harness.Ocr.Behavior = FakeOcrEngine.Recognizes(Letter);
        harness.Recognizer.Answer = Answer;

        Assert.Equal(2, await harness.ProcessAllAsync());

        var document = await harness.DocumentAsync(id);
        Assert.Equal((ProcessingState.Done, RecognitionState.Automatic), (document.State, document.Recognition));
        Assert.Equal(("Strom November 2026", Answer.Summary), (document.Title, document.Summary));
        Assert.Equal((Answer.DocumentDate, Answer.Amount), (document.DocumentDate, document.Amount));
        Assert.Single(document.Tags);
        Assert.Null(await harness.JobAsync(id));

        await using var db = harness.OtherWorkerContext();
        var correspondent = await db.Correspondents.AsNoTracking().SingleAsync(Ct);
        Assert.Equal((document.CorrespondentId, "Stadtwerke München", ProcessorHarness.Owner), (correspondent.Id, correspondent.Name, correspondent.CreatedBy));
        var request = Assert.Single(harness.Recognizer.Requests);
        Assert.StartsWith("Stadtwerke München GmbH", request.Text, StringComparison.Ordinal);
        Assert.Equal((0, 0), (request.Correspondents.Count, request.Examples.Count));
        Assert.Contains(5201, harness.RecognitionLog.EventIds);
        Assert.DoesNotContain(harness.RecognitionLog.Entries, entry => entry.Message.Contains("Strom", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Recognition_Waits_UntilTheModelIsReady()
    {
        await using var harness = await ProcessorHarness.CreateAsync(postgres, "recognition_not_ready");
        var waiting = new ProcessNextDocument(
            harness.Scopes, harness.Storage, harness.Ocr, harness.WorkingFolder, new RecognitionReadiness(), harness.Time, harness.Log);
        var id = await harness.AddQueuedDocumentAsync();

        Assert.True(await waiting.ProcessNextAsync(Ct));
        Assert.False(await waiting.ProcessNextAsync(Ct));

        Assert.Empty(harness.Recognizer.Requests);
        Assert.Equal((JobStage.Recognition, 0), ((await harness.JobAsync(id))!.Stage, (await harness.JobAsync(id))!.Attempt));
    }

    [Fact]
    public async Task Recognition_DropsValuesTheTextDoesNotBack()
    {
        await using var harness = await ProcessorHarness.CreateAsync(postgres, "recognition_checks");
        var id = await harness.AddQueuedDocumentAsync();
        harness.Ocr.Behavior = FakeOcrEngine.Recognizes(Letter);
        harness.Recognizer.Answer = Answer with { DocumentDate = new DateOnly(2023, 11, 22), Amount = new Money(99.99m, "EUR") };

        await harness.ProcessAllAsync();

        var document = await harness.DocumentAsync(id);
        Assert.Equal((RecognitionState.Automatic, null, null), (document.Recognition, document.DocumentDate, document.Amount));
        Assert.Equal(2, harness.RecognitionLog.EventIds.Count(eventId => eventId == 5206));
    }

    [Fact]
    public async Task Recognition_RetriesFailedAttempts_ThenGivesUp_AndTheDocumentStaysDone()
    {
        await using var harness = await ProcessorHarness.CreateAsync(postgres, "recognition_give_up");
        var id = await harness.AddQueuedDocumentAsync();
        harness.Recognizer.Behavior = FakeRecognizer.Throws(new RecognitionException("LLM service answered 500"));

        Assert.Equal(2, await harness.ProcessAllAsync());
        var job = await harness.JobAsync(id);
        Assert.Equal((JobStage.Recognition, 1, s_start.AddMinutes(1)), (job!.Stage, job.Attempt, job.NextAttemptAt));
        Assert.Equal("RecognitionException: LLM service answered 500", job.LastError);

        foreach (var minutes in new[] { 1, 6, 36 })
        {
            harness.Time.Now = s_start.AddMinutes(minutes);
            Assert.Equal(1, await harness.ProcessAllAsync());
        }

        Assert.Null(await harness.JobAsync(id));
        var document = await harness.DocumentAsync(id);
        Assert.Equal((ProcessingState.Done, RecognitionState.None, null), (document.State, document.Recognition, document.FailureReason));
        Assert.Equal(4, harness.Recognizer.Requests.Count);
        Assert.Contains(5204, harness.RecognitionLog.EventIds);
    }

    [Fact]
    public async Task Recognition_SkipsAConfirmedDocument_AndADocumentWithoutText()
    {
        await using var harness = await ProcessorHarness.CreateAsync(postgres, "recognition_skip");
        var confirmed = await harness.AddQueuedDocumentAsync();
        Assert.True(await harness.Processor.ProcessNextAsync(Ct));
        await using (var db = harness.OtherWorkerContext())
        {
            (await db.Documents.SingleAsync(document => document.Id == confirmed, Ct)).Confirm(ProcessorHarness.Owner);
            await db.SaveChangesAsync(Ct);
        }

        var empty = await harness.AddQueuedDocumentAsync();
        harness.Ocr.Behavior = FakeOcrEngine.Recognizes(" \n ");
        await harness.ProcessAllAsync();

        // The document without text never gets a recognition job; the confirmed one is skipped.
        Assert.Empty(harness.Recognizer.Requests);
        Assert.Equal((null, null), (await harness.JobAsync(confirmed), await harness.JobAsync(empty)));
        Assert.Equal(RecognitionState.Confirmed, (await harness.DocumentAsync(confirmed)).Recognition);
        Assert.Equal(1, harness.RecognitionLog.EventIds.Count(eventId => eventId == 5202));
    }

    [Fact]
    public async Task Recognition_DiscardsTheAnswer_WhenTheDocumentIsConfirmedMeanwhile()
    {
        await using var harness = await ProcessorHarness.CreateAsync(postgres, "recognition_confirmed_meanwhile");
        var id = await harness.AddQueuedDocumentAsync();
        harness.Recognizer.Behavior = async (_, cancellationToken) =>
        {
            await using var db = harness.OtherWorkerContext();
            (await db.Documents.SingleAsync(document => document.Id == id, cancellationToken)).Confirm(ProcessorHarness.Owner);
            await db.SaveChangesAsync(cancellationToken);
            return Answer;
        };

        Assert.Equal(2, await harness.ProcessAllAsync());

        var document = await harness.DocumentAsync(id);
        Assert.Equal((RecognitionState.Confirmed, "Scan 1", null), (document.Recognition, document.Title, document.CorrespondentId));
        await using var check = harness.OtherWorkerContext();
        Assert.False(await check.Correspondents.AnyAsync(Ct));
        Assert.Null(await harness.JobAsync(id));
        Assert.Contains(5205, harness.RecognitionLog.EventIds);
    }

    [Fact]
    public async Task Shutdown_DuringRecognition_HandsTheJobBack()
    {
        await using var harness = await ProcessorHarness.CreateAsync(postgres, "recognition_shutdown");
        var id = await harness.AddQueuedDocumentAsync();
        Assert.True(await harness.Processor.ProcessNextAsync(Ct));
        harness.Recognizer.Behavior = FakeRecognizer.WaitForCancellationAsync;
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(Ct);

        var run = harness.Processor.ProcessNextAsync(stopping.Token);
        await harness.Recognizer.Started.Task.WaitAsync(Ct);
        await stopping.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        var job = await harness.JobAsync(id);
        Assert.Equal((JobStage.Recognition, 0, (Guid?)null), (job!.Stage, job.Attempt, job.ClaimId));
        Assert.Contains(2108, harness.Log.EventIds);
        Assert.Equal(RecognitionState.None, (await harness.DocumentAsync(id)).Recognition);
    }
}
