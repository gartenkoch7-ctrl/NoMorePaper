using System.Runtime.CompilerServices;
using NoMorePaper.Application.Classification;
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
    /// With <see cref="EntryAudience.Household"/> only the entries a document that is not private uses:
    /// those every member sees already, whoever asks (ADR-0023, addendum 2026-11-19). An entry nobody
    /// uses yet is not among them, not even one of the viewer's own: it may be meant for private documents.
    /// The account is a parameter; table and column names are fixed text.
    /// </summary>
    public static FormattableString Entries(ClassificationKind kind, Guid viewerId, EntryAudience audience)
    {
        var (table, usages, column) = EntryTables.For(kind);

        // {0} is the viewer. A document without owner (M1, before setup) is never private.
        return audience switch
        {
            EntryAudience.Account => FormattableStringFactory.Create(
                $$"""
                SELECT e.* FROM {{table}} e
                WHERE EXISTS (SELECT 1 FROM {{usages}} WHERE {{column}} = e.id AND (d.owner_id = {0} OR NOT d.is_private))
                   OR (e.created_by = {0} AND NOT EXISTS (SELECT 1 FROM {{usages}} WHERE {{column}} = e.id))
                """,
                viewerId),
            EntryAudience.Household => FormattableStringFactory.Create(
                $$"""
                SELECT e.* FROM {{table}} e
                WHERE EXISTS (SELECT 1 FROM {{usages}} WHERE {{column}} = e.id AND NOT d.is_private)
                """),
            _ => throw new ArgumentOutOfRangeException(nameof(audience), audience, "Unknown audience."),
        };
    }
}
