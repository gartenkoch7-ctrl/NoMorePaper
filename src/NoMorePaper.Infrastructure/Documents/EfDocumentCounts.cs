using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Classification;
using NoMorePaper.Application.Documents;
using NoMorePaper.Infrastructure.Persistence;
using Npgsql;
using NpgsqlTypes;

namespace NoMorePaper.Infrastructure.Documents;

/// <summary>Counts for the document list in Postgres; shares the filter SQL with the list.</summary>
public sealed class EfDocumentCounts(AppDbContext db) : IDocumentCounts
{
    // Same rule as EfDocumentRepository.ListAsync: private documents of other accounts are never counted.
    // M4b puts the trash filter into both queries (here and in the list).
    private const string Visible = " WHERE (d.owner_id = @viewer OR NOT d.is_private)";

    public async Task<DocumentCounts> CountAsync(Guid viewerId, DocumentFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var withoutType = filter with { DocumentTypeId = null };
        var total = await CountMatchingAsync(viewerId, filter, cancellationToken).ConfigureAwait(false);
        var allTypes = filter.DocumentTypeId is null
            ? total
            : await CountMatchingAsync(viewerId, withoutType, cancellationToken).ConfigureAwait(false);

        // A new parameter list per query: an NpgsqlParameter belongs to one command only.
        var parameters = NewParameters(viewerId);
        var sql = """SELECT t.id AS "Id", t.name AS "Name", count(*)::int AS "Count" FROM documents d JOIN document_types t ON t.id = d.document_type_id"""
            + Visible + DocumentFilterSql.Conditions(withoutType, parameters) + " GROUP BY t.id, t.name ORDER BY t.name, t.id";
        var rows = await db.Database.SqlQueryRaw<DocumentTypeCountRow>(sql, [.. parameters])
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new DocumentCounts(total, allTypes, [.. rows.Select(row => new DocumentTypeCount(new EntrySummary(row.Id, row.Name), row.Count))]);
    }

    private async Task<int> CountMatchingAsync(Guid viewerId, DocumentFilter filter, CancellationToken cancellationToken)
    {
        var parameters = NewParameters(viewerId);
        var sql = """SELECT count(*)::int AS "Value" FROM documents d""" + Visible + DocumentFilterSql.Conditions(filter, parameters);
        return await db.Database.SqlQueryRaw<int>(sql, [.. parameters]).SingleAsync(cancellationToken).ConfigureAwait(false);
    }

    private static List<NpgsqlParameter> NewParameters(Guid viewerId) =>
        [new("viewer", NpgsqlDbType.Uuid) { Value = viewerId }];
}
