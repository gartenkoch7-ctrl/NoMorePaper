namespace NoMorePaper.Infrastructure.Search;

/// <summary>Shape of a search result row; property names equal the column aliases in <see cref="PostgresSearchIndex"/>.</summary>
internal sealed class SearchRow
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateTimeOffset ReceivedAt { get; set; }

    public string? Snippet { get; set; }

    public double Rank { get; set; }

    public bool IsPrivate { get; set; }

    public bool IsMine { get; set; }
}
