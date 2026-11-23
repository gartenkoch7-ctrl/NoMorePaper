using NoMorePaper.Application.Classification;
using NoMorePaper.Application.Editing;
using NoMorePaper.Domain.Editing;

namespace NoMorePaper.Application.Tests.Separation;

/// <summary>Stands in for the split of track A: answers <see cref="Outcome"/> and keeps what it was asked.</summary>
internal sealed class RecordingSplitRequester : ISplitRequester
{
    public ChangeOutcome Outcome { get; set; } = ChangeOutcome.Changed;

    public List<(Guid Id, SplitPlan Plan)> Requests { get; } = [];

    public Task<ChangeOutcome> RequestAsync(Guid id, SplitPlan plan, CancellationToken cancellationToken)
    {
        Requests.Add((id, plan));
        return Task.FromResult(Outcome);
    }
}
