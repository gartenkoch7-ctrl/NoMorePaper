namespace NoMorePaper.Application.Separation;

/// <summary>The local LLM's view of a scanned stack (<see cref="ISeparationAssessor"/>), checked for plausibility.</summary>
/// <param name="Boundaries">Pages where another document starts, ascending, each within the stack and after page 1.</param>
/// <param name="Senders">The sender the LLM read on each page, in page order; empty where it saw none.</param>
public sealed record SeparationAssessment(IReadOnlyList<int> Boundaries, IReadOnlyList<string> Senders);
