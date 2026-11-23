using System.Collections.Concurrent;
using NoMorePaper.Application.Classification;
using NoMorePaper.Application.Editing;
using NoMorePaper.Domain.Editing;

namespace NoMorePaper.Server.Tests.Editing;

/// <summary>Stands in for the split of M4b track A: queues nothing, answers <see cref="Outcome"/> and keeps what it was asked.</summary>
internal sealed class RecordingSplitRequester : ISplitRequester
{
    public ChangeOutcome Outcome { get; set; } = ChangeOutcome.Changed;

    public ConcurrentQueue<(Guid Id, SplitPlan Plan)> Requests { get; } = new();

    public Task<ChangeOutcome> RequestAsync(Guid id, SplitPlan plan, CancellationToken cancellationToken)
    {
        Requests.Enqueue((id, plan));
        return Task.FromResult(Outcome);
    }
}
