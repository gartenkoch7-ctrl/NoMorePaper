using System.Runtime.CompilerServices;
using NoMorePaper.Domain.Classification;

namespace NoMorePaper.Infrastructure.Classification;

/// <summary>
/// The one place of the visibility rule for list entries (M3 plan, ADR-0002): an entry is visible to an
/// account if a document the account may see uses it (own documents and all that are not private), or
/// if no document uses it and the account created it. An entry used only in private documents thus
/// stays as private as they are; once a shared document uses it, everybody sees it.
/// </summary>
public static class VisibilitySql
{
    /// <summary>
    /// A query of all columns of the entries of <paramref name="kind"/> that <paramref name="viewerId"/>
    /// may see, for <c>FromSql</c> on the entry's set; EF composes further filters and sorting around it.
    /// The account is a parameter; table and column names are fixed text.
    /// </summary>
    public static FormattableString Entries(ClassificationKind kind, Guid viewerId)
    {
        var (table, usages, usedBy) = kind switch
        {
            ClassificationKind.Correspondent => ("correspondents", "documents d", "d.correspondent_id = e.id"),
            ClassificationKind.DocumentType => ("document_types", "documents d", "d.document_type_id = e.id"),
            ClassificationKind.Tag => ("tags", "document_tags u JOIN documents d ON d.id = u.document_id", "u.tag_id = e.id"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown list."),
        };

        // {0} is the viewer. A document without owner (M1, before setup) is never private.
        var format = $$"""
            SELECT e.* FROM {{table}} e
            WHERE EXISTS (SELECT 1 FROM {{usages}} WHERE {{usedBy}} AND (d.owner_id = {0} OR NOT d.is_private))
               OR (e.created_by = {0} AND NOT EXISTS (SELECT 1 FROM {{usages}} WHERE {{usedBy}}))
            """;
        return FormattableStringFactory.Create(format, viewerId);
    }
}
