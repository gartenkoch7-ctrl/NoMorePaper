using System.Runtime.CompilerServices;
using NoMorePaper.Domain.Classification;

namespace NoMorePaper.Infrastructure.Classification;

/// <summary>
/// The statements of merging list entries: move every document from the sources to the target, then
/// delete the sources. Ids are parameters; table and column names are fixed text.
/// </summary>
internal static class EntryMergeSql
{
    /// <summary>Points the documents at the target; a document with a source tag and the target tag keeps one.</summary>
    public static FormattableString MoveDocuments(ClassificationKind kind, Guid targetId, Guid[] sourceIds)
    {
        var format = kind switch
        {
            ClassificationKind.Correspondent => "UPDATE documents SET correspondent_id = {0} WHERE correspondent_id = ANY({1})",
            ClassificationKind.DocumentType => "UPDATE documents SET document_type_id = {0} WHERE document_type_id = ANY({1})",
            ClassificationKind.Tag =>
                "INSERT INTO document_tags (document_id, tag_id) SELECT DISTINCT document_id, {0} FROM document_tags "
                + "WHERE tag_id = ANY({1}) ON CONFLICT DO NOTHING",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown list."),
        };
        return FormattableStringFactory.Create(format, targetId, sourceIds);
    }

    /// <summary>Deletes the sources; their remaining tag rows go with them (cascade).</summary>
    public static FormattableString DeleteEntries(ClassificationKind kind, Guid[] sourceIds) =>
        FormattableStringFactory.Create($"DELETE FROM {EntryTables.For(kind).Table} WHERE id = ANY({{0}})", sourceIds);
}
