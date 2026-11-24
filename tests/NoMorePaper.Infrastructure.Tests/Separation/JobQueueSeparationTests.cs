using NoMorePaper.Application.Documents;
using NoMorePaper.Application.Editing;
using NoMorePaper.Application.Processing;
using NoMorePaper.Domain.Editing;
using NoMorePaper.Infrastructure.Editing;
using NoMorePaper.Infrastructure.Tests.Persistence;
using NoMorePaper.Infrastructure.Tests.Processing;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Separation;

/// <summary>The separation stage in the queue: after OCR for scanned stacks, before recognition.</summary>
[Collection(SharedPostgres.Name)]
public sealed class JobQueueSeparationTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset s_start = QueueDatabase.Start;

    private static readonly StoredFile s_archive = new(4711, QueueDatabase.Sha256);

    private static readonly SplitSuggestion s_suggestion = new([new PageRange(1, 1), new PageRange(2, 3)], [3], [SplitReason.Salutation]);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static TheoryData<string, PageInfo[], JobStage> Stacks => new()
    {
        { "scan", [Page(1), Page(2, blank: true)], JobStage.Separation },
        { "single", [Page(1)], JobStage.Recognition },
        { "digital", [Page(1), Page(2, textLayer: true)], JobStage.Recognition },
        { "unknown", [], JobStage.Recognition },
    };

    [Theory]
    [MemberData(nameof(Stacks))]
    public async Task Complete_SendsAScannedStack_ToSeparation_EverythingElseToRecognition(string name, PageInfo[] pages, JobStage next)
    {
        var data = await QueueDatabase.CreateAsync(postgres, "queue_separation_switch_" + name);
        var id = await data.AddQueuedDocumentAsync(s_start);
        await using var db = data.Context();
        var queue = data.Queue(db);
        var claim = (await queue.ClaimNextAsync(QueueDatabase.LockFor, Ct))!;
        await queue.StartAsync(claim, Ct);

        Assert.True(await queue.CompleteAsync(claim, "Rechnung", s_archive, pages, Ct));

        var job = await data.JobAsync(id);
        Assert.Equal((next, 0, (Guid?)null, s_start), (job!.Stage, job.Attempt, job.ClaimId, job.NextAttemptAt));
    }

    [Fact]
    public async Task Complete_SendsAPartOfASplit_ToRecognition_EvenWhenItLooksLikeAStack()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "queue_separation_split_part");
        var id = await data.AddQueuedDocumentAsync(s_start);
        await using var db = data.Context();
        (await db.Documents.FindAsync([id], Ct))!.MarkSplitPart(Guid.CreateVersion7());
        await db.SaveChangesAsync(Ct);
        var queue = data.Queue(db);
        var claim = (await queue.ClaimNextAsync(QueueDatabase.LockFor, Ct))!;
        await queue.StartAsync(claim, Ct);

        Assert.True(await queue.CompleteAsync(claim, "Rechnung", s_archive, [Page(1), Page(2), Page(3, blank: true)], Ct));

        Assert.Equal(JobStage.Recognition, (await data.JobAsync(id))!.Stage);
    }

    [Fact]
    public async Task Complete_RemovesTheJob_OfAScannedStackWithoutText()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "queue_separation_no_text");
        var id = await data.AddQueuedDocumentAsync(s_start);
        await using var db = data.Context();
        var queue = data.Queue(db);
        var claim = (await queue.ClaimNextAsync(QueueDatabase.LockFor, Ct))!;
        await queue.StartAsync(claim, Ct);

        Assert.True(await queue.CompleteAsync(claim, " ", s_archive, [Page(1, blank: true), Page(2, blank: true)], Ct));

        Assert.Null(await data.JobAsync(id));
    }

    [Fact]
    public async Task ClaimNext_LeavesSeparation_WhileTheModelIsNotReady()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "queue_separation_not_ready");
        await ScannedStackAsync(data);
        await using var db = data.Context();

        Assert.Null(await data.Queue(db).ClaimNextAsync(QueueDatabase.LockFor, includeLlmStages: false, Ct));
        Assert.Equal(JobStage.Separation, (await data.Queue(db).ClaimNextAsync(QueueDatabase.LockFor, Ct))?.Stage);
    }

    [Fact]
    public async Task CompleteSeparation_NotesTheSuggestion_AndMovesOnToRecognition()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "queue_separation_complete");
        var id = await ScannedStackAsync(data);
        await using var db = data.Context();
        var queue = data.Queue(db);
        var claim = (await queue.ClaimNextAsync(QueueDatabase.LockFor, Ct))!;
        data.Time.Now = s_start.AddMinutes(2);

        Assert.True(await queue.CompleteSeparationAsync(claim, s_suggestion, Ct));

        var suggestion = (await data.DocumentAsync(id)).SplitSuggestion!;
        Assert.Equal(s_suggestion.Parts, suggestion.Parts);
        Assert.Equal(s_suggestion.BlankPages, suggestion.BlankPages);
        Assert.Equal(s_suggestion.Reasons, suggestion.Reasons);
        var job = await data.JobAsync(id);
        Assert.Equal((JobStage.Recognition, 0, (Guid?)null, s_start.AddMinutes(2)), (job!.Stage, job.Attempt, job.ClaimId, job.NextAttemptAt));
    }

    [Fact]
    public async Task CompleteSeparation_ChangesNothing_WhenTheClaimIsLost()
    {
        var data = await QueueDatabase.CreateAsync(postgres, "queue_separation_lost");
        var id = await ScannedStackAsync(data);
        await using var db = data.Context();
        var queue = data.Queue(db);
        var stale = (await queue.ClaimNextAsync(QueueDatabase.LockFor, Ct))!;
        Assert.True(await queue.ReleaseAsync(stale, Ct));

        Assert.False(await queue.CompleteSeparationAsync(stale, s_suggestion, Ct));

        Assert.Null((await data.DocumentAsync(id)).SplitSuggestion);
        Assert.Equal(JobStage.Separation, (await data.JobAsync(id))?.Stage);
    }

    // A done document whose OCR found a scanned stack: its job waits at the separation stage.
    private static async Task<Guid> ScannedStackAsync(QueueDatabase data)
    {
        var id = await data.AddQueuedDocumentAsync(s_start);
        await using var db = data.Context();
        var queue = data.Queue(db);
        var claim = (await queue.ClaimNextAsync(QueueDatabase.LockFor, Ct))!;
        await queue.StartAsync(claim, Ct);
        Assert.True(await queue.CompleteAsync(claim, "Rechnung", s_archive, [Page(1), Page(2), Page(3, blank: true)], Ct));
        Assert.Equal(3, (await new EfDocumentPages(db).ListAsync(id, Ct)).Count);
        return id;
    }

    private static PageInfo Page(int number, bool blank = false, bool textLayer = false) =>
        new(number, blank ? string.Empty : "Sehr geehrter Herr Mustermann,", blank, textLayer);
}
