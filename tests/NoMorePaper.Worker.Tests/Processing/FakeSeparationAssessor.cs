using NoMorePaper.Application.Editing;
using NoMorePaper.Application.Separation;

namespace NoMorePaper.Worker.Tests.Processing;

/// <summary>Stands in for the local LLM of the stack separation: answers with <see cref="Behavior"/> and counts its calls.</summary>
internal sealed class FakeSeparationAssessor : ISeparationAssessor
{
    /// <summary>What a call does; by default the LLM sees no boundary.</summary>
    public Func<IReadOnlyList<PageInfo>, SeparationAssessment> Behavior { get; set; } = _ => new SeparationAssessment([], []);

    public int Calls { get; private set; }

    public static Func<IReadOnlyList<PageInfo>, SeparationAssessment> Throws(Exception exception) => _ => throw exception;

    public Task<SeparationAssessment> AssessAsync(IReadOnlyList<PageInfo> pages, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(Behavior(pages));
    }
}
