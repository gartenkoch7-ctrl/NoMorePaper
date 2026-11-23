namespace NoMorePaper.Domain.Editing;

/// <summary>
/// The pages of an edited document in their new order: a page left out is deleted, a page listed is
/// turned and possibly replaced. Pure logic.
/// </summary>
/// <param name="Steps">The pages in their new order.</param>
public sealed record PagePlan(IReadOnlyList<PageStep> Steps)
{
    /// <summary>
    /// Whether the plan fits a source of <paramref name="sourcePageCount"/> pages: at least one page,
    /// each page of the source at most once, known rotations only.
    /// </summary>
    public bool Validate(int sourcePageCount) => Fits(sourcePageCount, new HashSet<int>());

    // Shared with SplitPlan, which counts the pages of all parts in one set.
    internal bool Fits(int sourcePageCount, HashSet<int> used)
    {
        if (Steps is null || Steps.Count == 0)
        {
            return false;
        }

        foreach (var step in Steps)
        {
            if (step is null || step.SourcePage < 1 || step.SourcePage > sourcePageCount
                || !Enum.IsDefined(step.Rotation) || !used.Add(step.SourcePage))
            {
                return false;
            }
        }

        return true;
    }
}
