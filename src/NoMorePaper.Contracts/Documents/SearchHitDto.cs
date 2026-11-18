namespace NoMorePaper.Contracts.Documents;

/// <summary>One search result, best first.</summary>
/// <param name="Snippet">Plain-text excerpt around the match; clients must never render it as HTML.</param>
/// <param name="Rank">Relevance; higher is better, only comparable within one search.</param>
/// <param name="IsPrivate">Only the owner sees a private document; marked in the result list.</param>
public sealed record SearchHitDto(
    Guid Id,
    string Title,
    DateTimeOffset ReceivedAt,
    string Snippet,
    double Rank,
    bool IsPrivate = false);
