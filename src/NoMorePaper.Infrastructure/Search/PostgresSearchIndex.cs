using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Documents;
using NoMorePaper.Application.Search;
using NoMorePaper.Infrastructure.Documents;
using NoMorePaper.Infrastructure.Persistence;
using NoMorePaper.Infrastructure.Telemetry;
using Npgsql;
using NpgsqlTypes;

namespace NoMorePaper.Infrastructure.Search;

/// <summary>
/// Full-text search over finished documents: German and English stems first, trigram similarity as
/// fallback for words the OCR misread. All user text reaches Postgres as a parameter, never as SQL.
/// </summary>
public sealed class PostgresSearchIndex(AppDbContext db, ILogger<PostgresSearchIndex> logger) : ISearchIndex
{
    // Below 0.4, trigram matches are mostly noise. SET takes no parameters, hence a fixed statement.
    private const string SetSimilarityThreshold = "SET LOCAL pg_trgm.word_similarity_threshold = 0.4";

    // Columns every hit carries besides snippet and rank. @viewer is the account searching.
    private const string HitColumns = """
        d.id AS "Id", d.title AS "Title", d.received_at AS "ReceivedAt",
        d.is_private AS "IsPrivate", d.owner_id IS NOT DISTINCT FROM @viewer AS "IsMine", d.edited_at AS "EditedAt",
        """ + DocumentFilterSql.LabelColumns;

    // Only finished documents the viewer may see, never those in the trash; the filters of the list narrow it further.
    private const string Visible = " WHERE d.state = 'Done' AND " + TrashSql.NotInTrash + " AND (d.owner_id = @viewer OR NOT d.is_private)";

    private static readonly Histogram<double> s_duration = InfrastructureTelemetry.Meter.CreateHistogram<double>(
        "nomorepaper.search.duration", unit: "ms", description: "Time to answer a search");

    /// <returns>
    /// Best hits first. Only documents in state <c>Done</c> that <paramref name="viewerId"/> may see are
    /// searched; the visibility filter is part of the SQL, like everywhere else.
    /// </returns>
    public async Task<IReadOnlyList<SearchHit>> SearchAsync(
        string text, Guid viewerId, int limit, DocumentFilter filter, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentNullException.ThrowIfNull(filter);
        using var activity = InfrastructureTelemetry.Source.StartActivity("nomorepaper.search.query");
        var started = Stopwatch.GetTimestamp();

        var hits = await StemSearchAsync(text, viewerId, limit, filter, cancellationToken).ConfigureAwait(false);
        var fallback = hits.Count == 0;
        if (fallback)
        {
            hits = await SimilaritySearchAsync(text, viewerId, limit, filter, cancellationToken).ConfigureAwait(false);
        }

        var elapsed = Stopwatch.GetElapsedTime(started);
        s_duration.Record(elapsed.TotalMilliseconds);
        logger.SearchCompleted(hits.Count, (long)elapsed.TotalMilliseconds, fallback);
        return hits;
    }

    // Snippets use the German configuration; they are the start of the text when only the English stem matched.
    private async Task<List<SearchHit>> StemSearchAsync(
        string text, Guid viewerId, int limit, DocumentFilter filter, CancellationToken cancellationToken)
    {
        var parameters = Parameters(text, viewerId, limit);
        var sql = """
            WITH q AS (SELECT websearch_to_tsquery('german', @text) || websearch_to_tsquery('english', @text) AS tsq)
            SELECT ts_headline('german', d.content_text, q.tsq,
                       'StartSel=, StopSel=, MaxWords=30, MinWords=15, MaxFragments=1') AS "Snippet",
                   ts_rank(d.search_vector, q.tsq)::float8 AS "Rank",
            """ + HitColumns + " FROM documents d CROSS JOIN q " + DocumentFilterSql.LabelJoins
            + Visible + " AND d.search_vector @@ q.tsq" + DocumentFilterSql.Conditions(filter, parameters)
            + """ ORDER BY "Rank" DESC, d.received_at DESC LIMIT @limit""";

        var rows = await db.Database.SqlQueryRaw<SearchRow>(sql, [.. parameters]).ToListAsync(cancellationToken).ConfigureAwait(false);
        return ToHits(rows);
    }

    // Only the operator form (<%) can use the trigram index; a word_similarity(...) > x filter reads
    // every row. The operator takes its threshold from the setting, so it is set for this transaction only.
    private async Task<List<SearchHit>> SimilaritySearchAsync(
        string text, Guid viewerId, int limit, DocumentFilter filter, CancellationToken cancellationToken)
    {
        var parameters = Parameters(text, viewerId, limit);
        var sql = """
            SELECT left(d.content_text, 200) AS "Snippet",
                   word_similarity(@text, d.content_text)::float8 AS "Rank",
            """ + HitColumns + " FROM documents d " + DocumentFilterSql.LabelJoins
            + Visible + " AND @text <% d.content_text" + DocumentFilterSql.Conditions(filter, parameters)
            + " ORDER BY @text <<-> d.content_text, d.received_at DESC LIMIT @limit";

        var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            await db.Database.ExecuteSqlRawAsync(SetSimilarityThreshold, cancellationToken).ConfigureAwait(false);
            var rows = await db.Database.SqlQueryRaw<SearchRow>(sql, [.. parameters]).ToListAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ToHits(rows);
        }
    }

    // New parameters per statement: a parameter belongs to one command.
    private static List<NpgsqlParameter> Parameters(string text, Guid viewerId, int limit) =>
    [
        new("text", NpgsqlDbType.Text) { Value = text },
        new("viewer", NpgsqlDbType.Uuid) { Value = viewerId },
        new("limit", NpgsqlDbType.Integer) { Value = limit },
    ];

    private static List<SearchHit> ToHits(List<SearchRow> rows) =>
        [.. rows.Select(row => new SearchHit(
            row.Id, row.Title, row.ReceivedAt, row.Snippet ?? string.Empty, row.Rank, row.IsPrivate, row.IsMine, row.ToLabels(), row.EditedAt))];
}
