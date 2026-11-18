using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Search;
using NoMorePaper.Infrastructure.Persistence;
using NoMorePaper.Infrastructure.Telemetry;

namespace NoMorePaper.Infrastructure.Search;

/// <summary>
/// Full-text search over finished documents: German and English stems first, trigram similarity as
/// fallback for words the OCR misread. All user text reaches Postgres as a parameter, never as SQL.
/// </summary>
public sealed class DocumentSearch(AppDbContext db, ILogger<DocumentSearch> logger)
{
    public const int MaxLimit = 50;

    // Longer queries only cost time; nobody searches with a paragraph.
    private const int MaxQueryLength = 200;

    // Below 0.4, trigram matches are mostly noise. SET takes no parameters, hence a fixed statement.
    private const string SetSimilarityThreshold = "SET LOCAL pg_trgm.word_similarity_threshold = 0.4";

    private static readonly Histogram<double> s_duration = InfrastructureTelemetry.Meter.CreateHistogram<double>(
        "nomorepaper.search.duration", unit: "ms", description: "Time to answer a search");

    /// <returns>Best hits first; empty for an empty query. Only documents in state <c>Done</c> are searched.</returns>
    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int limit, CancellationToken cancellationToken)
    {
        var text = Normalize(query);
        if (text.Length == 0)
        {
            return [];
        }

        using var activity = InfrastructureTelemetry.Source.StartActivity("nomorepaper.search.query");
        var size = Math.Clamp(limit, 1, MaxLimit);
        var started = Stopwatch.GetTimestamp();

        var hits = await StemSearchAsync(text, size, cancellationToken).ConfigureAwait(false);
        var fallback = hits.Count == 0;
        if (fallback)
        {
            hits = await SimilaritySearchAsync(text, size, cancellationToken).ConfigureAwait(false);
        }

        var elapsed = Stopwatch.GetElapsedTime(started);
        s_duration.Record(elapsed.TotalMilliseconds);
        logger.SearchCompleted(hits.Count, (long)elapsed.TotalMilliseconds, fallback);
        return hits;
    }

    // Snippets use the German configuration; they are the start of the text when only the English stem matched.
    private async Task<List<SearchHit>> StemSearchAsync(string text, int limit, CancellationToken cancellationToken)
    {
        var rows = await db.Database.SqlQuery<SearchRow>($"""
            WITH q AS (SELECT websearch_to_tsquery('german', {text}) || websearch_to_tsquery('english', {text}) AS tsq)
            SELECT d.id AS "Id", d.title AS "Title", d.received_at AS "ReceivedAt",
                   ts_headline('german', d.content_text, q.tsq,
                       'StartSel=, StopSel=, MaxWords=30, MinWords=15, MaxFragments=1') AS "Snippet",
                   ts_rank(d.search_vector, q.tsq)::float8 AS "Rank"
            FROM documents d CROSS JOIN q
            WHERE d.state = 'Done' AND d.search_vector @@ q.tsq
            ORDER BY "Rank" DESC, d.received_at DESC
            LIMIT {limit}
            """).ToListAsync(cancellationToken).ConfigureAwait(false);

        return ToHits(rows);
    }

    // Only the operator form (<%) can use the trigram index; a word_similarity(...) > x filter reads
    // every row. The operator takes its threshold from the setting, so it is set for this transaction only.
    private async Task<List<SearchHit>> SimilaritySearchAsync(string text, int limit, CancellationToken cancellationToken)
    {
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            await db.Database.ExecuteSqlRawAsync(SetSimilarityThreshold, cancellationToken).ConfigureAwait(false);
            var rows = await db.Database.SqlQuery<SearchRow>($"""
                SELECT d.id AS "Id", d.title AS "Title", d.received_at AS "ReceivedAt",
                       left(d.content_text, 200) AS "Snippet",
                       word_similarity({text}, d.content_text)::float8 AS "Rank"
                FROM documents d
                WHERE d.state = 'Done' AND {text} <% d.content_text
                ORDER BY {text} <<-> d.content_text, d.received_at DESC
                LIMIT {limit}
                """).ToListAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return ToHits(rows);
        }
    }

    private static List<SearchHit> ToHits(List<SearchRow> rows) =>
        [.. rows.Select(row => new SearchHit(row.Id, row.Title, row.ReceivedAt, row.Snippet ?? string.Empty, row.Rank))];

    // NUL cannot be stored in Postgres text and would fail the whole request.
    private static string Normalize(string? query)
    {
        var text = string.Concat((query ?? string.Empty).Where(c => c != '\0')).Trim();
        return text.Length > MaxQueryLength ? text[..MaxQueryLength] : text;
    }
}
