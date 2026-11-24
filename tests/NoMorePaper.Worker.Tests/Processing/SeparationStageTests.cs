using NoMorePaper.Application.Editing;
using NoMorePaper.Application.Processing;
using NoMorePaper.Application.Recognition;
using NoMorePaper.Application.Separation;
using NoMorePaper.Domain.Editing;
using NoMorePaper.Domain.Processing;
using NoMorePaper.Worker.Tests.Persistence;
using Xunit;

namespace NoMorePaper.Worker.Tests.Processing;

/// <summary>A scanned stack goes from OCR through the separation stage to recognition; nothing is split.</summary>
[Collection(SharedPostgres.Name)]
public sealed class SeparationStageTests(PostgresFixture postgres)
{
    private static readonly PageInfo[] s_stack =
    [
        new(1, "Stadtwerke München\nSehr geehrter Herr Mustermann,", false, false),
        new(2, "der Abschlag wird am 15.11.2026 abgebucht.", false, false),
        new(3, "AOK Bayern\nSehr geehrte Damen und Herren,", false, false),
    ];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AScannedStack_GetsASuggestion_AndIsRecognizedAfterwards()
    {
        await using var harness = await StackAsync("separation_flow");
        var id = await harness.AddQueuedDocumentAsync();
        harness.Separator.Behavior = _ => new SeparationAssessment([2], ["Stadtwerke München", "", "AOK Bayern"]);

        Assert.Equal(3, await harness.ProcessAllAsync());

        var suggestion = (await harness.DocumentAsync(id)).SplitSuggestion!;
        Assert.Equal([new PageRange(1, 1), new PageRange(2, 2), new PageRange(3, 3)], suggestion.Parts);
        Assert.Equal([SplitReason.Salutation, SplitReason.SenderChange], suggestion.Reasons);
        Assert.Equal(1, harness.Separator.Calls);
        Assert.Single(harness.Recognizer.Requests);
        Assert.Null(await harness.JobAsync(id));
    }

    [Fact]
    public async Task WithoutAnAnswerOfTheLlm_ThePageSignsAloneCount()
    {
        await using var harness = await StackAsync("separation_llm_failed");
        var id = await harness.AddQueuedDocumentAsync();
        harness.Separator.Behavior = FakeSeparationAssessor.Throws(new InvalidDataException("cut"));

        Assert.Equal(3, await harness.ProcessAllAsync());

        Assert.Equal([new PageRange(1, 2), new PageRange(3, 3)], (await harness.DocumentAsync(id)).SplitSuggestion!.Parts);
        Assert.Single(harness.Recognizer.Requests);
    }

    [Fact]
    public async Task OneDocument_GetsNoSuggestion()
    {
        await using var harness = await StackAsync("separation_none");
        harness.Pages.Pages = [s_stack[0], s_stack[1]];
        var id = await harness.AddQueuedDocumentAsync();

        Assert.Equal(3, await harness.ProcessAllAsync());

        Assert.Null((await harness.DocumentAsync(id)).SplitSuggestion);
        Assert.Equal(1, harness.Separator.Calls);
    }

    [Fact]
    public async Task ABusyLlm_HandsTheJobBack_WithoutCountingTheAttempt()
    {
        await using var harness = await StackAsync("separation_llm_busy");
        var id = await harness.AddQueuedDocumentAsync();
        harness.Separator.Behavior = FakeSeparationAssessor.Throws(new LlmBusyException());
        Assert.True(await harness.Processor.ProcessNextAsync(Ct));

        Assert.True(await harness.Processor.ProcessNextAsync(Ct));

        var job = await harness.JobAsync(id);
        Assert.Equal((JobStage.Separation, 0, (Guid?)null, (string?)null), (job!.Stage, job.Attempt, job.ClaimId, job.LastError));
        Assert.Null((await harness.DocumentAsync(id)).SplitSuggestion);
        Assert.Empty(harness.Recognizer.Requests);
    }

    [Fact]
    public async Task AStageInterruptedInEveryAttempt_LetsRecognitionFollow_WithoutSuggestion()
    {
        await using var harness = await StackAsync("separation_interrupted");
        var id = await harness.AddQueuedDocumentAsync();
        Assert.True(await harness.Processor.ProcessNextAsync(Ct));
        await using (var db = harness.OtherWorkerContext())
        {
            var job = await db.ProcessingJobs.FindAsync([id], Ct);
            job!.Attempt = RetryPolicy.MaxAttempts;
            await db.SaveChangesAsync(Ct);
        }

        Assert.True(await harness.Processor.ProcessNextAsync(Ct));

        Assert.Equal((JobStage.Recognition, 0), ((await harness.JobAsync(id))!.Stage, (await harness.JobAsync(id))!.Attempt));
        Assert.Null((await harness.DocumentAsync(id)).SplitSuggestion);
        Assert.Equal(0, harness.Separator.Calls);
    }

    private async Task<ProcessorHarness> StackAsync(string database)
    {
        var harness = await ProcessorHarness.CreateAsync(postgres, database);
        harness.Pages.Pages = s_stack;
        return harness;
    }
}
