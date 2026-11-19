using System.Runtime.CompilerServices;
using NoMorePaper.Domain.Classification;

namespace NoMorePaper.Infrastructure.Classification;

/// <summary>
/// The rights rule for renaming and merging list entries (M3 plan): allowed only for an account that
/// may edit <b>every</b> document using one of the entries, as one SQL check. "May edit" is
/// <c>Document.CanEdit</c>: the owner, or a chosen editor while the document is not private; a document
/// without owner (M1) nobody. Account and ids are parameters; table and column names are fixed text.
/// </summary>
internal static class EntryRightsSql
{
    /// <summary>
    /// Locks the entries until the transaction ends. A document that starts using one of them takes a key
    /// share lock on it, so no document can join between the rights check and the change. Locked in id
    /// order, so two merges of overlapping entries wait for each other instead of deadlocking.
    /// </summary>
    public static FormattableString Lock(ClassificationKind kind, Guid[] entryIds)
    {
        var tables = EntryTables.For(kind);
        return FormattableStringFactory.Create($"SELECT id FROM {tables.Table} WHERE id = ANY({{0}}) ORDER BY id FOR UPDATE", entryIds);
    }

    /// <summary>
    /// One row, column <c>Value</c>: whether a document using one of <paramref name="entryIds"/> exists
    /// that <paramref name="actorId"/> may not edit.
    /// </summary>
    public static FormattableString AnyDocumentNotEditable(ClassificationKind kind, Guid actorId, Guid[] entryIds)
    {
        var tables = EntryTables.For(kind);

        // {0} is the actor, {1} the entry ids.
        var format = $$"""
            SELECT EXISTS (
                SELECT 1 FROM {{tables.Usages}}
                WHERE {{tables.Column}} = ANY({1})
                  AND NOT (d.owner_id IS NOT NULL AND (d.owner_id = {0} OR (NOT d.is_private AND EXISTS (
                      SELECT 1 FROM document_editors ed WHERE ed.document_id = d.id AND ed.account_id = {0}))))
            ) AS "Value"
            """;
        return FormattableStringFactory.Create(format, actorId, entryIds);
    }
}
