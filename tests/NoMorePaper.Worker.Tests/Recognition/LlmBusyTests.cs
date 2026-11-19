using NoMorePaper.Application.Processing;
using NoMorePaper.Application.Recognition;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Worker.Tests.Persistence;
using NoMorePaper.Worker.Tests.Processing;
using Xunit;

namespace NoMorePaper.Worker.Tests.Recognition;

/// <summary>A recognition that waited too long for the shared LLM (mail checks) hands its job back without counting.</summary>
[Collection(SharedPostgres.Name)]
public sealed class LlmBusyTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ABusyLlm_HandsTheJobBack_WithoutCountingTheAttempt()
    {
        await using var harness = await ProcessorHarness.CreateAsync(postgres, "recognition_llm_busy");
        var id = await harness.AddQueuedDocumentAsync();
        harness.Ocr.Behavior = FakeOcrEngine.Recognizes(RecognitionStageTests.Letter);
        Assert.True(await harness.Processor.ProcessNextAsync(Ct));

        harness.Recognizer.Behavior = FakeRecognizer.Throws(new LlmBusyException());
        Assert.True(await harness.Processor.ProcessNextAsync(Ct));

        var job = await harness.JobAsync(id);
        Assert.Equal((JobStage.Recognition, 0, (Guid?)null, (string?)null), (job!.Stage, job.Attempt, job.ClaimId, job.LastError));
        Assert.True(job.NextAttemptAt <= harness.Time.GetUtcNow());
        Assert.Contains(5214, harness.RecognitionLog.EventIds);
        Assert.DoesNotContain(5203, harness.RecognitionLog.EventIds);

        harness.Recognizer.Behavior = null;
        harness.Recognizer.Answer = RecognitionStageTests.Answer;
        Assert.Equal(1, await harness.ProcessAllAsync());
        Assert.Equal(RecognitionState.Automatic, (await harness.DocumentAsync(id)).Recognition);
    }
}
