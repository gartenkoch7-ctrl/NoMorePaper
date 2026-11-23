namespace NoMorePaper.Contracts.Documents;

/// <summary>Counts of the documents the signed-in account may see, for the list header and the type chips.</summary>
/// <param name="Total">Documents matching all filters.</param>
/// <param name="AllTypes">Documents matching all filters except the document type filter.</param>
/// <param name="ByType">Per document type, without the type filter; only types with at least one document, sorted by name.</param>
public sealed record DocumentCountsDto(int Total, int AllTypes, IReadOnlyList<DocumentTypeCountDto> ByType);
