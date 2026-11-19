using NoMorePaper.Application.Inputs;

namespace NoMorePaper.Infrastructure.Inputs;

/// <summary>A row of <c>input_seen_items</c>: only the SHA-256 of the item's key, never the key itself.</summary>
internal sealed class SeenItemRow
{
    public InputKind Kind { get; set; }

    public Guid AccountKey { get; set; }

    public Guid? AccountId { get; set; }

    public SeenList List { get; set; }

    public string KeyHash { get; set; } = string.Empty;

    public int Failures { get; set; }

    public DateTimeOffset SeenAt { get; set; }
}
