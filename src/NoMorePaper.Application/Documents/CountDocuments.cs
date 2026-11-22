using NoMorePaper.Application.Accounts;

namespace NoMorePaper.Application.Documents;

/// <summary>Counts the documents the signed-in account may see, with the filters of the list.</summary>
public sealed class CountDocuments(IDocumentCounts counts, ICurrentAccount current)
{
    public Task<DocumentCounts> CountAsync(DocumentFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        return counts.CountAsync(current.Id, filter, cancellationToken);
    }
}
