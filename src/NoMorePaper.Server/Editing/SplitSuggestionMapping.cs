using NoMorePaper.Contracts.Editing;
using NoMorePaper.Domain.Editing;

namespace NoMorePaper.Server.Editing;

/// <summary>Maps a split suggestion to its Contracts DTO; the reasons stay on the server. Pure logic.</summary>
internal static class SplitSuggestionMapping
{
    public static SplitSuggestionDto ToDto(this SplitSuggestion suggestion) =>
        new([.. suggestion.Parts.Select(part => new PageRangeDto(part.First, part.Last))], [.. suggestion.BlankPages]);
}
