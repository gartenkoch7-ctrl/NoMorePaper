namespace NoMorePaper.Contracts.Editing;

/// <summary>Consecutive pages, both ends included, starting at 1.</summary>
public sealed record PageRangeDto(int First, int Last);
