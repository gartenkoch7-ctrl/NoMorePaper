namespace NoMorePaper.Web.Client.Documents;

/// <summary>Arithmetic of the pager of the document list; pure so it can be tested.</summary>
public static class PageWindow
{
    private const int AllShownUpTo = 7;

    public static int TotalPages(int total, int pageSize) =>
        Math.Max(1, (int)Math.Ceiling(total / (double)Math.Max(1, pageSize)));

    /// <summary>First and last row number (1-based) shown on <paramref name="page"/>; <c>(0, 0)</c> without rows.</summary>
    public static (int From, int To) Range(int page, int pageSize, int total) =>
        total <= 0 ? (0, 0) : ((page - 1) * pageSize + 1, Math.Min(page * pageSize, total));

    /// <summary>The page numbers to offer; <c>null</c> is a gap ("…"). <paramref name="current"/> is limited to the valid pages.</summary>
    public static IReadOnlyList<int?> Pages(int current, int totalPages)
    {
        var last = Math.Max(1, totalPages);
        var page = Math.Clamp(current, 1, last);
        if (last <= AllShownUpTo)
        {
            return [.. Enumerable.Range(1, last).Select(number => (int?)number)];
        }

        if (page <= 3)
        {
            return [1, 2, 3, 4, null, last];
        }

        if (page >= last - 2)
        {
            return [1, null, last - 3, last - 2, last - 1, last];
        }

        return [1, null, page - 1, page, page + 1, null, last];
    }
}
