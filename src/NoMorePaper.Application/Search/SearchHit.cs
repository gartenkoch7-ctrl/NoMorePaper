using NoMorePaper.Application.Documents;

namespace NoMorePaper.Application.Search;

/// <summary>One search result. <paramref name="Snippet"/> is plain text; <paramref name="Rank"/> is only comparable within one search.</summary>
/// <param name="Labels">Correspondent, type, date, amount and recognition state.</param>
/// <param name="EditedAt">When its newest edited version was made; <c>null</c> without one.</param>
public sealed record SearchHit(
    Guid Id, string Title, DateTimeOffset ReceivedAt, string Snippet, double Rank, bool IsPrivate, bool IsMine, DocumentLabels Labels,
    DateTimeOffset? EditedAt = null);
