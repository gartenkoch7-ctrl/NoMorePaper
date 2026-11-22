using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Documents;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Persistence;

namespace NoMorePaper.Infrastructure.Documents;

/// <summary>
/// Reads the OCR text from Postgres. Visibility is the same as in <c>EfDocumentRepository</c>
/// (own or not private); LINQ instead of SQL, so a global query filter such as the trash of M4b applies by itself.
/// </summary>
internal sealed class EfDocumentTextReader(AppDbContext db) : IDocumentTextReader
{
    public async Task<DocumentText?> GetAsync(Guid id, Guid viewerId, CancellationToken cancellationToken)
    {
        var row = await db.Documents.AsNoTracking()
            .Where(d => d.Id == id && (d.OwnerId == viewerId || !d.IsPrivate))
            .Select(d => new { d.State, d.ContentText })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return null;
        }

        return row.State == ProcessingState.Done ? new DocumentText(row.ContentText ?? string.Empty) : new DocumentText(null);
    }
}
