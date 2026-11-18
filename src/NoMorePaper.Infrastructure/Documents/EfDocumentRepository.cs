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

    public async Task<IReadOnlyList<DocumentSummary>> ListAsync(Guid viewerId, int page, int pageSize, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        var skip = (long)(page - 1) * pageSize;

        var rows = await VisibleTo(viewerId)
            .OrderByDescending(document => document.ReceivedAt)
            .ThenByDescending(document => document.Id)
            .Skip((int)Math.Min(skip, int.MaxValue))
            .Take(pageSize)
            .Select(document => new { document.Id, document.Title, document.ReceivedAt, document.State, document.IsPrivate, IsMine = document.OwnerId == viewerId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. rows.Select(row => new DocumentSummary(row.Id, row.Title, row.ReceivedAt, row.State, row.IsPrivate, row.IsMine))];
    }

    public async Task<DocumentDetail?> GetAsync(Guid id, Guid viewerId, CancellationToken cancellationToken)
    {
        var document = await VisibleTo(viewerId)
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
            document.FindFile(DocumentFileKind.Archive),
            document.IsPrivate,
            document.OwnerId == viewerId);
    }

    public async Task<bool> SetPrivacyAsync(Guid id, Guid ownerId, bool isPrivate, CancellationToken cancellationToken)
    {
        // Looked up by id and owner together: another account's document is "not found", never "forbidden".
        var document = await db.Documents
            .SingleOrDefaultAsync(found => found.Id == id && found.OwnerId == ownerId, cancellationToken)
            .ConfigureAwait(false);
        if (document is null)
        {
            return false;
        }

        if (isPrivate)
        {
            document.MakePrivate(ownerId);
        }
        else
        {
            document.Share(ownerId);
        }

        // Only is_private changes, so a Worker saving the processing state meanwhile is not overwritten.
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    // The visibility rule of Document.IsVisibleTo, as SQL: private documents never leave the database
    // for anybody but their owner.
    private IQueryable<Document> VisibleTo(Guid viewerId) =>
        db.Documents.AsNoTracking().Where(document => document.OwnerId == viewerId || !document.IsPrivate);
}
