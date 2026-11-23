using NoMorePaper.Domain.Classification;
using NoMorePaper.Infrastructure.Persistence;

namespace NoMorePaper.Infrastructure.Classification;

/// <summary>
/// Fixed table and column names of one list, for the plain SQL of rights and merging. Never built from
/// input: <see cref="For"/> only knows the three lists. Pure logic.
/// </summary>
/// <param name="Table">The list's table, e.g. <c>correspondents</c>.</param>
/// <param name="Usages">
/// Rows that use an entry, joined to <c>documents d</c>: the documents themselves, or <c>document_tags u</c>.
/// Documents in the trash use no entry here (<see cref="TrashSql"/>), so an entry only they use is as
/// visible as one nobody uses, and they do not count for the rights of renaming and merging.
/// </param>
/// <param name="Column">The column of <paramref name="Usages"/> that holds the entry id.</param>
internal sealed record EntryTables(string Table, string Usages, string Column)
{
    private const string Documents = "(SELECT * FROM documents d WHERE " + TrashSql.NotInTrash + ") d";

    public static EntryTables For(ClassificationKind kind) => kind switch
    {
        ClassificationKind.Correspondent => new("correspondents", Documents, "d.correspondent_id"),
        ClassificationKind.DocumentType => new("document_types", Documents, "d.document_type_id"),
        ClassificationKind.Tag => new("tags", "document_tags u JOIN " + Documents + " ON d.id = u.document_id", "u.tag_id"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown list."),
    };
}
