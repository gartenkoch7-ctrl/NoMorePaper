using Microsoft.EntityFrameworkCore;
using NoMorePaper.Infrastructure.Persistence;

namespace NoMorePaper.Infrastructure.Classification;

/// <summary>
/// Loads what turns <see cref="FieldsRow"/>s into views: entry names and duplicate candidates. Entries
/// are looked up by id without the visibility rule, because a document the viewer may see uses them,
/// which makes them visible; candidates are filtered by the viewer, so another account's private
/// document never appears as a duplicate.
/// </summary>
internal sealed class FieldsReader(AppDbContext db)
{
    public async Task<FieldsLookup> LoadAsync(IReadOnlyCollection<FieldsRow> rows, Guid viewerId, CancellationToken cancellationToken)
    {
        var candidateIds = rows.Select(row => row.PossibleDuplicateOf).OfType<Guid>().Distinct().ToList();
        var candidates = candidateIds.Count == 0
            ? []
            : await db.Documents.AsNoTracking()
                .Where(document => candidateIds.Contains(document.Id) && (document.OwnerId == viewerId || !document.IsPrivate))
                .Select(document => new CandidateRow(
                    document.Id, document.Title, document.ReceivedAt, document.DocumentDate, document.CorrespondentId))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        var correspondentIds = rows.Select(row => row.CorrespondentId)
            .Concat(candidates.Select(candidate => candidate.CorrespondentId))
            .OfType<Guid>().Distinct().ToList();
        var typeIds = rows.Select(row => row.DocumentTypeId).OfType<Guid>().Distinct().ToList();
        var tagIds = rows.SelectMany(row => row.TagIds).Distinct().ToList();

        return new FieldsLookup(
            await NamesAsync(db.Correspondents, correspondentIds, cancellationToken).ConfigureAwait(false),
            await NamesAsync(db.DocumentTypes, typeIds, cancellationToken).ConfigureAwait(false),
            await NamesAsync(db.Tags, tagIds, cancellationToken).ConfigureAwait(false),
            candidates.ToDictionary(candidate => candidate.Id));
    }

    private static async Task<IReadOnlyDictionary<Guid, string>> NamesAsync<TEntry>(
        IQueryable<TEntry> entries, List<Guid> ids, CancellationToken cancellationToken)
        where TEntry : Domain.Classification.ClassificationEntry
    {
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await entries.AsNoTracking()
            .Where(entry => ids.Contains(entry.Id))
            .ToDictionaryAsync(entry => entry.Id, entry => entry.Name, cancellationToken)
            .ConfigureAwait(false);
    }
}
