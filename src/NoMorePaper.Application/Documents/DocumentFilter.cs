namespace NoMorePaper.Application.Documents;

/// <summary>
/// Filters of the document list and the search; every <c>null</c> filter is off. They only narrow the
/// documents the viewer may see anyway, so an entry id of another account's private list finds nothing.
/// </summary>
/// <param name="From">Earliest document date, inclusive.</param>
/// <param name="To">Latest document date, inclusive.</param>
/// <param name="TagIds">Tags to look for; <c>null</c> or empty means no tag filter.</param>
/// <param name="AllTags">Whether a document needs all of <paramref name="TagIds"/> instead of any of them.</param>
/// <param name="AmountFrom">Smallest amount, inclusive, whatever the currency.</param>
/// <param name="AmountTo">Largest amount, inclusive, whatever the currency.</param>
/// <param name="IsPrivate"><c>true</c> for private documents only, <c>false</c> for shared ones only.</param>
public sealed record DocumentFilter(
    Guid? CorrespondentId = null,
    Guid? DocumentTypeId = null,
    DateOnly? From = null,
    DateOnly? To = null,
    IReadOnlyList<Guid>? TagIds = null,
    bool AllTags = false,
    decimal? AmountFrom = null,
    decimal? AmountTo = null,
    bool? IsPrivate = null)
{
    /// <summary>No filter: all documents the viewer may see.</summary>
    public static DocumentFilter None { get; } = new();

    /// <summary>The distinct tag ids to look for; empty when the tag filter is off.</summary>
    public IReadOnlyList<Guid> DistinctTagIds => TagIds is null ? [] : [.. TagIds.Distinct()];
}
