using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Classification;
using NoMorePaper.Application.Documents;
using NoMorePaper.Application.Editing;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Editing;
using NoMorePaper.Infrastructure.Inputs;
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

    // Recorded at all, in the trash or not: the intake asks whether its commit went through.
    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) =>
        db.Documents.AsNoTracking().IgnoreQueryFilters([TrashSql.FilterName]).AnyAsync(d => d.Id == id, cancellationToken);

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
        // rule of Document.IsVisibleTo (trash included) comes first, the filters only narrow it.
        var sql = """
            SELECT d.id AS "Id", d.title AS "Title", d.received_at AS "ReceivedAt", d.state AS "State",
                   d.is_private AS "IsPrivate", d.owner_id IS NOT DISTINCT FROM @viewer AS "IsMine",
                   d.source AS "Source", d.edited_at AS "EditedAt",
                   left(regexp_replace(left(coalesce(nullif(btrim(d.summary, E' \t\r\n'), ''), d.content_text), 2000), '\s+', ' ', 'g'), 400) AS "Excerpt",
            """ + DocumentFilterSql.LabelColumns + " FROM documents d " + DocumentFilterSql.LabelJoins
            + " WHERE " + TrashSql.NotInTrash + " AND (d.owner_id = @viewer OR NOT d.is_private)" + DocumentFilterSql.Conditions(filter, parameters)
            + " ORDER BY d.received_at DESC, d.id DESC OFFSET @skip LIMIT @take";

        var rows = await db.Database.SqlQueryRaw<DocumentListRow>(sql, [.. parameters])
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var tags = await LoadTagsAsync([.. rows.Select(row => row.Id)], cancellationToken).ConfigureAwait(false);
        return [.. rows.Select(row => new DocumentSummary(
            row.Id, row.Title, row.ReceivedAt, Enum.Parse<ProcessingState>(row.State), row.IsPrivate, row.IsMine, row.ToLabels(),
            Enum.Parse<DocumentSource>(row.Source), DocumentExcerpt.From(row.Excerpt), [.. tags[row.Id]], row.EditedAt))];
    }

    // Only for the documents of the page, which the viewer may see already, so no visibility check is needed here.
    private async Task<ILookup<Guid, EntrySummary>> LoadTagsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return Enumerable.Empty<EntrySummary>().ToLookup(_ => Guid.Empty);
        }

        var rows = await db.Database.SqlQueryRaw<DocumentTagRow>(
                """
                SELECT dt.document_id AS "DocumentId", tg.id AS "TagId", tg.name AS "TagName"
                FROM document_tags dt JOIN tags tg ON tg.id = dt.tag_id
                WHERE dt.document_id = ANY(@ids) ORDER BY tg.name, tg.id
                """,
                new NpgsqlParameter("ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = ids.ToArray() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.ToLookup(row => row.DocumentId, row => new EntrySummary(row.TagId, row.TagName));
    }

    public Task<Guid?> FindVisibleBySha256Async(Guid viewerId, string sha256, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);
        return FindBySha256Async(sha256, "(d.owner_id = {1} OR NOT d.is_private)", viewerId, cancellationToken);
    }

    public Task<Guid?> FindOwnedBySha256Async(Guid ownerId, string sha256, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);
        return FindBySha256Async(sha256, "d.owner_id = {1}", ownerId, cancellationToken);
    }

    // kind = 'Original' as literal and the hash as character(64), so the partial index on originals is used.
    // Only originals that exist count: one in the trash, or of a document in the trash, is no measure.
    // {0} is the hash, {1} the account of the fixed condition.
    private async Task<Guid?> FindBySha256Async(string sha256, string accountCondition, Guid accountId, CancellationToken cancellationToken)
    {
        var found = await db.Database.SqlQuery<Guid>(FormattableStringFactory.Create(
            $$"""
            SELECT d.id AS "Value"
            FROM document_files f JOIN documents d ON d.id = f.document_id
            WHERE f.kind = 'Original' AND f.sha256 = CAST({0} AS character(64)) AND {{TrashSql.NotInTrash}}
              AND {{accountCondition}}
            ORDER BY d.received_at, d.id
            LIMIT 1
            """,
            sha256,
            accountId)).ToListAsync(cancellationToken).ConfigureAwait(false);
        return found.Count == 0 ? null : found[0];
    }

    public async Task<DocumentDetail?> GetAsync(Guid id, Guid viewerId, CancellationToken cancellationToken)
    {
        var document = await VisibleTo(viewerId)
            .SingleOrDefaultAsync(found => found.Id == id, cancellationToken)
            .ConfigureAwait(false);

        // A document always has a source (original or edited version); one without is not served rather than half shown.
        if (document?.CurrentSource is null)
        {
            return null;
        }

        var edit = await db.Set<DocumentEditRow>().AsNoTracking()
            .Where(row => row.DocumentId == id)
            .Select(row => new { row.Kind, row.Data })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        var policy = document.VersionPolicy ?? await OwnerDefaultAsync(document.OwnerId, cancellationToken).ConfigureAwait(false);
        return new DocumentDetail(
            document.Id,
            document.Title,
            document.ReceivedAt,
            document.State,
            document.FailureReason,
            document.FindFile(DocumentFileKind.Original),
            document.FindFile(DocumentFileKind.Archive),
            document.IsPrivate,
            document.OwnerId == viewerId,
            document.FindFile(DocumentFileKind.Edited),
            document.EditedAt,
            edit is not null,
            policy,
            edit is not null && EditDataJson.Parse(edit.Data, edit.Kind) is { IsCommitted: true });
    }

    // The owner's default for edited documents; the owner decides, whoever looks.
    private async Task<VersionPolicy> OwnerDefaultAsync(Guid? ownerId, CancellationToken cancellationToken) =>
        await db.Set<AccountPreferencesRow>().AsNoTracking()
            .Where(row => row.AccountId == ownerId)
            .Select(row => (VersionPolicy?)row.AfterEditing)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false) ?? VersionPolicy.KeepBoth;

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
    // for anybody but their owner; the trash filter of the model hides documents in the trash.
    private IQueryable<Document> VisibleTo(Guid viewerId) =>
        db.Documents.AsNoTracking().Where(document => document.OwnerId == viewerId || !document.IsPrivate);
}
