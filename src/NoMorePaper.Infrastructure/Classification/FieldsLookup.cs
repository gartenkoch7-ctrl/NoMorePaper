using NoMorePaper.Application.Classification;

namespace NoMorePaper.Infrastructure.Classification;

/// <summary>
/// Names of the entries and the visible duplicate candidates for a set of <see cref="FieldsRow"/>s,
/// loaded once by <see cref="FieldsReader"/>, so a list of documents costs a fixed number of queries.
/// </summary>
internal sealed class FieldsLookup(
    IReadOnlyDictionary<Guid, string> correspondents,
    IReadOnlyDictionary<Guid, string> documentTypes,
    IReadOnlyDictionary<Guid, string> tags,
    IReadOnlyDictionary<Guid, CandidateRow> candidates)
{
    public DocumentFieldsView View(FieldsRow row, Guid viewerId) =>
        new(
            Entry(correspondents, row.CorrespondentId),
            Entry(documentTypes, row.DocumentTypeId),
            row.DocumentDate,
            row.Amount,
            row.Summary,
            [.. row.TagIds.Select(id => Entry(tags, id)).OfType<EntrySummary>().OrderBy(tag => tag.Name, StringComparer.InvariantCultureIgnoreCase)],
            row.Recognition,
            Candidate(row)?.Id,
            row.EditorIds,
            row.CanEdit(viewerId));

    /// <returns>The document <paramref name="row"/> may duplicate, if the viewer may see it.</returns>
    public DuplicateCandidate? Candidate(FieldsRow row) =>
        row.PossibleDuplicateOf is { } id && candidates.TryGetValue(id, out var candidate)
            ? new DuplicateCandidate(candidate.Id, candidate.Title, candidate.ReceivedAt, candidate.DocumentDate, Entry(correspondents, candidate.CorrespondentId))
            : null;

    private static EntrySummary? Entry(IReadOnlyDictionary<Guid, string> names, Guid? id) =>
        id is { } key && names.TryGetValue(key, out var name) ? new EntrySummary(key, name) : null;
}
