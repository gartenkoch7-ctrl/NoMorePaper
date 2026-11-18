namespace NoMorePaper.Application.Search;

/// <summary>One search result. <paramref name="Snippet"/> is plain text; <paramref name="Rank"/> is only comparable within one search.</summary>
public sealed record SearchHit(Guid Id, string Title, DateTimeOffset ReceivedAt, string Snippet, double Rank);
