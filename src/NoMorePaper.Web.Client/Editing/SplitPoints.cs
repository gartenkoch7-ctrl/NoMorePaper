namespace NoMorePaper.Web.Client.Editing;

/// <summary>Which split points of the page editor really divide the document. Pure logic.</summary>
internal static class SplitPoints
{
    /// <summary>
    /// The split points whose closed part keeps a page and that have a kept page somewhere behind them.
    /// A point that only closes deleted pages, or has only deleted pages behind it, changes nothing.
    /// </summary>
    public static IReadOnlySet<int> Effective(IReadOnlyList<PageEntry> entries, IReadOnlySet<int> splits)
    {
        var lastKept = -1;
        for (var i = 0; i < entries.Count; i++)
        {
            if (!entries[i].Deleted)
            {
                lastKept = i;
            }
        }

        var effective = new HashSet<int>();
        var partKept = false;
        for (var i = 0; i < entries.Count; i++)
        {
            partKept |= !entries[i].Deleted;
            if (!splits.Contains(i))
            {
                continue;
            }

            if (partKept && i < lastKept)
            {
                effective.Add(i);
            }

            partKept = false;
        }

        return effective;
    }
}
