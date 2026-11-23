namespace NoMorePaper.Contracts.Editing;

/// <summary>Where a scanned stack probably splits into several documents; nothing is split before it is accepted.</summary>
/// <param name="Parts">The suggested documents, in order.</param>
/// <param name="BlankPages">Probably blank pages; left out when the stack is split.</param>
public sealed record SplitSuggestionDto(IReadOnlyList<PageRangeDto> Parts, IReadOnlyList<int> BlankPages);
