namespace NoMorePaper.Application.Documents;

/// <summary>Counts for the document list.</summary>
public interface IDocumentCounts
{
    /// <summary>Counts only documents <paramref name="viewerId"/> may see - the same rule as <see cref="IDocumentRepository.ListAsync"/>.</summary>
    Task<DocumentCounts> CountAsync(Guid viewerId, DocumentFilter filter, CancellationToken cancellationToken);
}
