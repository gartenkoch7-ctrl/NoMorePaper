using NoMorePaper.Application.Documents;
using NoMorePaper.Application.Processing;
using NoMorePaper.Application.Recognition;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Tests.Classification;

/// <summary>A job queue that only records requeued recognitions, for the hand-over.</summary>
internal sealed class RequeueOnlyJobQueue : IJobQueue
{
    public List<Guid> Requeued { get; } = [];

    /// <summary>What <see cref="RequeueRecognitionAsync"/> answers (e.g. <c>false</c> while OCR still runs).</summary>
    public bool RequeueResult { get; set; } = true;

    public Task<bool> RequeueRecognitionAsync(Guid documentId, CancellationToken cancellationToken)
    {
        Requeued.Add(documentId);
        return Task.FromResult(RequeueResult);
    }

    public Task<JobClaim?> ClaimNextAsync(TimeSpan lockFor, bool includeRecognition, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<Document?> StartAsync(JobClaim claim, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<bool> CompleteAsync(JobClaim claim, string text, StoredFile archive, CancellationToken cancellationToken) =>
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
