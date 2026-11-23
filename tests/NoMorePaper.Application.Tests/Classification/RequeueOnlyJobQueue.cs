using NoMorePaper.Application.Documents;
using NoMorePaper.Application.Editing;
using NoMorePaper.Application.Processing;
using NoMorePaper.Application.Recognition;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Tests.Classification;

/// <summary>A job queue that only records requeued recognitions (hand-over) and dropped jobs (trash).</summary>
internal sealed class RequeueOnlyJobQueue : IJobQueue
{
    public List<Guid> Requeued { get; } = [];

    /// <summary>What <see cref="RequeueRecognitionAsync"/> answers (e.g. <c>false</c> while OCR still runs).</summary>
    public bool RequeueResult { get; set; } = true;

    /// <summary>Documents whose job <see cref="CancelAsync"/> dropped.</summary>
    public List<Guid> Cancelled { get; } = [];

    /// <summary>What <see cref="CancelAsync"/> answers: whether there was a job.</summary>
    public bool HadJob { get; set; }

    public Task<bool> RequeueRecognitionAsync(Guid documentId, CancellationToken cancellationToken)
    {
        Requeued.Add(documentId);
        return Task.FromResult(RequeueResult);
    }

    public Task<bool> CancelAsync(Guid documentId, CancellationToken cancellationToken)
    {
        Cancelled.Add(documentId);
        return Task.FromResult(HadJob);
    }

    public Task<JobClaim?> ClaimNextAsync(TimeSpan lockFor, bool includeLlmStages, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<Document?> StartAsync(JobClaim claim, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<bool> CompleteAsync(
        JobClaim claim, string text, StoredFile archive, IReadOnlyList<PageInfo> pages, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    /// <summary>Documents whose edit <see cref="EnqueueEditAsync"/> was asked to queue.</summary>
    public List<Guid> EditsQueued { get; } = [];

    /// <summary>What <see cref="EnqueueEditAsync"/> answers (<c>false</c>: another job runs or waits).</summary>
    public bool EnqueueEditResult { get; set; } = true;

    public Task<bool> EnqueueEditAsync(Guid documentId, bool recognizeAfter, CancellationToken cancellationToken)
    {
        EditsQueued.Add(documentId);
        return Task.FromResult(EnqueueEditResult);
    }

    /// <summary>What <see cref="DropPendingAnalysisAsync"/> answers.</summary>
    public bool HadPendingAnalysis { get; set; }

    public Task<bool> DropPendingAnalysisAsync(Guid documentId, CancellationToken cancellationToken) => Task.FromResult(HadPendingAnalysis);

    public Task<Document?> ReadDocumentAsync(Guid documentId, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<EditCommitResult> CompleteEditAsync(JobClaim claim, EditCompletion completion, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<bool> FinishEditAsync(JobClaim claim, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<bool> CompleteAnalysisAsync(JobClaim claim, IReadOnlyList<PageInfo> pages, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<int> EnqueuePageBacklogAsync(int priority, DateTimeOffset now, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<RecordedFailure> FailAsync(JobClaim claim, string technicalError, bool permanent, DateTimeOffset now, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<bool> ReleaseAsync(JobClaim claim, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<Document?> ReadAsync(JobClaim claim, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<RecognitionCommit> CompleteRecognitionAsync(
        JobClaim claim, DocumentFields? fields, bool wasPrivate, Guid? wasOwner, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<int> EnqueueRecognitionBacklogAsync(int priority, DateTimeOffset now, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
