namespace NoMorePaper.Infrastructure.Persistence;

/// <summary>
/// The trash filter of plain SQL (ADR-0025): a document in the trash is invisible everywhere but on its
/// owner's trash page. EF queries get it from the named query filter <see cref="FilterName"/>; every plain
/// query over <c>documents</c> uses this class (checked by the architecture tests). Pure logic.
/// </summary>
internal static class TrashSql
{
    /// <summary>Name of the EF query filter on documents; trash queries switch it off with <c>IgnoreQueryFilters</c>.</summary>
    public const string FilterName = "NotInTrash";

    /// <summary>The condition for a query whose documents are aliased <c>d</c>.</summary>
    public const string NotInTrash = "d.deleted_at IS NULL";

    /// <summary>The condition for documents aliased <paramref name="alias"/>, a fixed name of the query, never input.</summary>
    public static string NotInTrashOf(string alias)
    {
        if (string.IsNullOrEmpty(alias) || !alias.All(char.IsAsciiLetterLower))
        {
            throw new ArgumentException("A table alias of lowercase letters.", nameof(alias));
        }

        return $"{alias}.deleted_at IS NULL";
    }
}
