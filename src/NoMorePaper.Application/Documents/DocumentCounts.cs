namespace NoMorePaper.Application.Documents;

/// <summary>Counts of the documents a viewer may see; see <see cref="IDocumentCounts"/>.</summary>
/// <param name="Total">Documents matching all filters.</param>
/// <param name="AllTypes">Documents matching all filters except the document type.</param>
/// <param name="ByType">Per document type, without the type filter; only types with documents, sorted by name.</param>
public sealed record DocumentCounts(int Total, int AllTypes, IReadOnlyList<DocumentTypeCount> ByType);
