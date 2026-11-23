namespace NoMorePaper.Domain.Editing;

/// <summary>
/// How a document is split: one page plan per new document, in order. Pages listed in no part are
/// deleted; each part may turn and replace its pages like an edit. Pure logic.
/// </summary>
/// <param name="Parts">The new documents' pages.</param>
public sealed record SplitPlan(IReadOnlyList<PagePlan> Parts)
{
    /// <summary>
    /// Whether the plan fits a source of <paramref name="pageCount"/> pages: at least two parts, each a
    /// valid page plan, and each page of the source in at most one part.
    /// </summary>
    public bool Validate(int pageCount)
    {
        if (Parts is null || Parts.Count < 2)
        {
            return false;
        }

        var used = new HashSet<int>();
        return Parts.All(part => part is not null && part.Fits(pageCount, used));
    }
}
