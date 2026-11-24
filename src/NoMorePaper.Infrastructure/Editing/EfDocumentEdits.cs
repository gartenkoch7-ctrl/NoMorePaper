using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Editing;
using NoMorePaper.Infrastructure.Persistence;

namespace NoMorePaper.Infrastructure.Editing;

/// <summary>
/// Planned edits in Postgres. Each write is one statement, so it joins the caller's open transaction
/// (e.g. the change that locked the document) and leaves tracked entities alone.
/// </summary>
public sealed class EfDocumentEdits(AppDbContext db) : IDocumentEdits
{
    public async Task SaveAsync(DocumentEdit edit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(edit);
        var kind = edit.Kind.ToString();
        var requestedAt = edit.RequestedAt.ToUniversalTime();
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO document_edits (document_id, kind, data, requested_by, requested_at, recognize_after)
            VALUES ({edit.DocumentId}, {kind}, CAST({edit.Data} AS jsonb), {edit.RequestedBy}, {requestedAt}, {edit.RecognizeAfter})
            ON CONFLICT (document_id) DO UPDATE SET
                kind = EXCLUDED.kind, data = EXCLUDED.data, requested_by = EXCLUDED.requested_by,
                requested_at = EXCLUDED.requested_at, recognize_after = EXCLUDED.recognize_after
            """,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<DocumentEdit?> GetAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var row = await db.Set<DocumentEditRow>().AsNoTracking()
            .SingleOrDefaultAsync(found => found.DocumentId == documentId, cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : new DocumentEdit(row.DocumentId, row.Kind, row.Data, row.RequestedBy, row.RequestedAt, row.RecognizeAfter);
    }

    public Task DeleteAsync(Guid documentId, CancellationToken cancellationToken) =>
        db.Set<DocumentEditRow>().Where(row => row.DocumentId == documentId).ExecuteDeleteAsync(cancellationToken);

    public async Task<bool> UpdateDataAsync(Guid documentId, string data, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(data);
        return await db.Database.ExecuteSqlAsync(
            $"UPDATE document_edits SET data = CAST({data} AS jsonb) WHERE document_id = {documentId}",
            cancellationToken).ConfigureAwait(false) > 0;
    }
}
