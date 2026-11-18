using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Documents;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Persistence;
using NoMorePaper.Infrastructure.Processing;

namespace NoMorePaper.Infrastructure.Documents;

/// <summary>
/// Documents in Postgres. Returns Application read models; the Server maps them to Contracts DTOs,
/// because Infrastructure must not reference Contracts (AGENTS.md 2.2).
/// </summary>
public sealed class EfDocumentRepository(AppDbContext db) : IDocumentRepository
{
    public async Task AddReceivedAsync(Document document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        db.Documents.Add(document);
        db.ProcessingJobs.Add(new ProcessingJob { DocumentId = document.Id, Attempt = 0, NextAttemptAt = document.ReceivedAt });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) =>
        db.Documents.AsNoTracking().AnyAsync(d => d.Id == id, cancellationToken);

    public async Task<IReadOnlyList<DocumentSummary>> ListAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        var skip = (long)(page - 1) * pageSize;

        var rows = await db.Documents.AsNoTracking()
            .OrderByDescending(document => document.ReceivedAt)
            .ThenByDescending(document => document.Id)
            .Skip((int)Math.Min(skip, int.MaxValue))
            .Take(pageSize)
            .Select(document => new { document.Id, document.Title, document.ReceivedAt, document.State })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. rows.Select(row => new DocumentSummary(row.Id, row.Title, row.ReceivedAt, row.State))];
    }

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
