using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Editing;
using NoMorePaper.Infrastructure.Persistence;

namespace NoMorePaper.Infrastructure.Editing;

/// <summary>The page information of documents in Postgres; written by the queue with the archive.</summary>
public sealed class EfDocumentPages(AppDbContext db) : IDocumentPages
{
    public async Task<IReadOnlyList<PageInfo>> ListAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Set<DocumentPageRow>().AsNoTracking()
            .Where(row => row.DocumentId == id)
            .OrderBy(row => row.Page)
            .Select(row => new PageInfo(row.Page, row.Head, row.ProbablyBlank, row.HadTextLayer))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
