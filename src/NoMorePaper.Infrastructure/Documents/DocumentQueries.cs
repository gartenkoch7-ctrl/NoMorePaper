using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Documents;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Persistence;

namespace NoMorePaper.Infrastructure.Documents;

/// <summary>
/// Read side of the document list and detail view. Returns Application read models; the Server maps
/// them to Contracts DTOs, because Infrastructure must not reference Contracts (AGENTS.md 2.2).
/// </summary>
public sealed class DocumentQueries(AppDbContext db)
{
    public const int MaxPageSize = 100;

    /// <summary>Newest first. A page below 1 means the first page; <paramref name="pageSize"/> is clamped to 1–100.</summary>
    public async Task<IReadOnlyList<DocumentSummary>> ListAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var size = Math.Clamp(pageSize, 1, MaxPageSize);
        var skip = (long)(Math.Max(page, 1) - 1) * size;

        var rows = await db.Documents.AsNoTracking()
            .OrderByDescending(document => document.ReceivedAt)
            .ThenByDescending(document => document.Id)
            .Skip((int)Math.Min(skip, int.MaxValue))
            .Take(size)
            .Select(document => new { document.Id, document.Title, document.ReceivedAt, document.State })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. rows.Select(row => new DocumentSummary(row.Id, row.Title, row.ReceivedAt, row.State))];
    }

    /// <returns>The document, or <c>null</c> if it does not exist.</returns>
    public async Task<DocumentDetail?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var document = await db.Documents.AsNoTracking()
            .SingleOrDefaultAsync(found => found.Id == id, cancellationToken)
            .ConfigureAwait(false);

        // A document always has its original; one without it is not served rather than half shown.
        if (document?.FindFile(DocumentFileKind.Original) is not { } original)
        {
            return null;
        }

        return new DocumentDetail(
            document.Id,
            document.Title,
            document.ReceivedAt,
            document.State,
            document.FailureReason,
            original,
            document.FindFile(DocumentFileKind.Archive));
    }
}
