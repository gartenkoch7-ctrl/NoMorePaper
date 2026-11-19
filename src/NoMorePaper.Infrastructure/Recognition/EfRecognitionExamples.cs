using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Recognition;
using NoMorePaper.Domain.Classification;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Classification;
using NoMorePaper.Infrastructure.Persistence;

namespace NoMorePaper.Infrastructure.Recognition;

/// <summary>
/// Confirmed documents as recognition examples, from Postgres. Only documents the owner may see (own
/// ones and all that are not private) are read, filtered in the query; their entries are visible to the
/// owner through them, so the names reveal nothing either.
/// </summary>
public sealed class EfRecognitionExamples(AppDbContext db) : IRecognitionExamples
{
    public async Task<IReadOnlyList<RecognitionExample>> FindAsync(
        Guid ownerId,
        IReadOnlyList<Guid> correspondentIds,
        Guid exceptDocumentId,
        int limit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(correspondentIds);
        var ids = correspondentIds.Distinct().ToArray();
        if (ids.Length == 0 || limit <= 0)
        {
            return [];
        }

        // One query for the order: the first correspondent's documents first, newest first within one.
        var confirmed = nameof(RecognitionState.Confirmed);
        var chosen = await db.Database.SqlQuery<Guid>(
                $"""
                SELECT ranked.id AS "Value" FROM (
                    SELECT d.id, d.received_at, array_position({ids}, d.correspondent_id) AS mention,
                           row_number() OVER (PARTITION BY d.correspondent_id ORDER BY d.received_at DESC, d.id DESC) AS n
                    FROM documents d
                    WHERE d.correspondent_id = ANY({ids}) AND d.recognition = {confirmed} AND d.id <> {exceptDocumentId}
                      AND (d.owner_id = {ownerId} OR NOT d.is_private)) ranked
                WHERE ranked.n <= {limit}
                ORDER BY ranked.mention, ranked.received_at DESC, ranked.id DESC
                LIMIT {limit}
                """)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (chosen.Count == 0)
        {
            return [];
        }

        var rows = await db.Documents.AsNoTracking()
            .Where(document => chosen.Contains(document.Id))
            .Select(document => new ExampleRow(
                document.Id, document.CorrespondentId, document.DocumentTypeId, document.Title, document.Tags.Select(tag => tag.TagId).ToList()))
            .ToDictionaryAsync(row => row.Id, cancellationToken)
            .ConfigureAwait(false);
        var ordered = chosen.Where(rows.ContainsKey).Select(id => rows[id]).ToList();

        var correspondents = await NamesAsync(ClassificationKind.Correspondent, ordered.Select(row => row.CorrespondentId).OfType<Guid>(), cancellationToken)
            .ConfigureAwait(false);
        var types = await NamesAsync(ClassificationKind.DocumentType, ordered.Select(row => row.DocumentTypeId).OfType<Guid>(), cancellationToken)
            .ConfigureAwait(false);
        var tags = await NamesAsync(ClassificationKind.Tag, ordered.SelectMany(row => row.TagIds), cancellationToken).ConfigureAwait(false);

        // The queries see no common snapshot: a document whose correspondent was merged away or cleared
        // in between is left out.
        return [.. ordered
            .Where(row => row.CorrespondentId is { } id && correspondents.ContainsKey(id))
            .Select(row => new RecognitionExample(
                correspondents[row.CorrespondentId!.Value],
                row.DocumentTypeId is { } typeId ? types.GetValueOrDefault(typeId) : null,
                [.. row.TagIds.Select(tags.GetValueOrDefault).OfType<string>().Order(StringComparer.Ordinal)],
                row.Title))];
    }

    private async Task<Dictionary<Guid, string>> NamesAsync(ClassificationKind kind, IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var wanted = ids.Distinct().ToList();
        return wanted.Count == 0
            ? []
            : await EntrySets.All(db, kind).AsNoTracking()
                .Where(entry => wanted.Contains(entry.Id))
                .ToDictionaryAsync(entry => entry.Id, entry => entry.Name, cancellationToken)
                .ConfigureAwait(false);
    }

    private sealed record ExampleRow(Guid Id, Guid? CorrespondentId, Guid? DocumentTypeId, string Title, List<Guid> TagIds);
}
