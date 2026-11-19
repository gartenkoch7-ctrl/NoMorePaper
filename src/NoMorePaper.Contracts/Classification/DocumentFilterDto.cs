namespace NoMorePaper.Contracts.Classification;

/// <summary>Filters for the document list and the search; every <c>null</c> filter is off.</summary>
/// <param name="From">Earliest document date, inclusive.</param>
/// <param name="To">Latest document date, inclusive.</param>
/// <param name="TagIds">Tags to look for.</param>
/// <param name="AllTags">Whether a document needs all of <paramref name="TagIds"/> instead of any of them.</param>
/// <param name="AmountFrom">Smallest amount, inclusive.</param>
/// <param name="AmountTo">Largest amount, inclusive.</param>
/// <param name="IsPrivate"><c>true</c> for private documents only, <c>false</c> for shared ones only.</param>
public sealed record DocumentFilterDto(
    Guid? CorrespondentId = null,
    Guid? DocumentTypeId = null,
    DateOnly? From = null,
    DateOnly? To = null,
    IReadOnlyList<Guid>? TagIds = null,
    bool AllTags = false,
    decimal? AmountFrom = null,
    decimal? AmountTo = null,
    bool? IsPrivate = null);
