using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Documents;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Persistence;
using NoMorePaper.Infrastructure.Processing;
using Npgsql;
using NpgsqlTypes;

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

    public async Task<IReadOnlyList<DocumentSummary>> ListAsync(
        Guid viewerId, int page, int pageSize, DocumentFilter filter, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentNullException.ThrowIfNull(filter);
        List<NpgsqlParameter> parameters =
        [
            new("viewer", NpgsqlDbType.Uuid) { Value = viewerId },
            new("skip", NpgsqlDbType.Bigint) { Value = (long)(page - 1) * pageSize },
            new("take", NpgsqlDbType.Integer) { Value = pageSize },
        ];

        // Plain SQL so list and search share one filter definition (DocumentFilterSql); the visibility
        // rule of Document.IsVisibleTo comes first, the filters only narrow it.
        var sql = """
            SELECT d.id AS "Id", d.title AS "Title", d.received_at AS "ReceivedAt", d.state AS "State",
                   d.is_private AS "IsPrivate", d.owner_id IS NOT DISTINCT FROM @viewer AS "IsMine",
            """ + DocumentFilterSql.LabelColumns + " FROM documents d " + DocumentFilterSql.LabelJoins
            + " WHERE (d.owner_id = @viewer OR NOT d.is_private)" + DocumentFilterSql.Conditions(filter, parameters)
            + " ORDER BY d.received_at DESC, d.id DESC OFFSET @skip LIMIT @take";

        var rows = await db.Database.SqlQueryRaw<DocumentListRow>(sql, [.. parameters])
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. rows.Select(row => new DocumentSummary(
            row.Id, row.Title, row.ReceivedAt, Enum.Parse<ProcessingState>(row.State), row.IsPrivate, row.IsMine, row.ToLabels()))];
    }

    public async Task<Guid?> FindVisibleBySha256Async(Guid viewerId, string sha256, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);

        // kind = 'Original' as literal and the hash as character(64), so the partial index on originals is used.
        var found = await db.Database.SqlQuery<Guid>($"""
            SELECT d.id AS "Value"
            FROM document_files f JOIN documents d ON d.id = f.document_id
            WHERE f.kind = 'Original' AND f.sha256 = CAST({sha256} AS character(64))
              AND (d.owner_id = {viewerId} OR NOT d.is_private)
            ORDER BY d.received_at, d.id
            LIMIT 1
            """).ToListAsync(cancellationToken).ConfigureAwait(false);
        return found.Count == 0 ? null : found[0];
    }

    public async Task<Guid?> FindOwnedBySha256Async(Guid ownerId, string sha256, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);
        var found = await db.Database.SqlQuery<Guid>($"""
            SELECT d.id AS "Value"
            FROM document_files f JOIN documents d ON d.id = f.document_id
            WHERE f.kind = 'Original' AND f.sha256 = CAST({sha256} AS character(64)) AND d.owner_id = {ownerId}
            ORDER BY d.received_at, d.id
            LIMIT 1
            """).ToListAsync(cancellationToken).ConfigureAwait(false);
        return found.Count == 0 ? null : found[0];
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
