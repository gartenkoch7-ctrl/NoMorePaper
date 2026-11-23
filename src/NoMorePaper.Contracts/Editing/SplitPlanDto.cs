namespace NoMorePaper.Contracts.Editing;

/// <summary>How a document is split: one page plan per new document, at least two (<c>POST …/split</c>).</summary>
public sealed record SplitPlanDto(IReadOnlyList<PagePlanDto> Parts);
