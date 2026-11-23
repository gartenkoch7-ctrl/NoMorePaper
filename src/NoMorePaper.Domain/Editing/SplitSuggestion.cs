namespace NoMorePaper.Domain.Editing;

/// <summary>
/// Where a scanned stack probably splits into several documents; only a suggestion, nothing is split
/// before the owner confirms it. Pure logic.
/// </summary>
/// <param name="Parts">The suggested documents, in order.</param>
/// <param name="BlankPages">Probably blank pages; they are left out when the stack is split.</param>
/// <param name="Reasons">Why the stack seems to be several documents.</param>
public sealed record SplitSuggestion(IReadOnlyList<PageRange> Parts, IReadOnlyList<int> BlankPages, IReadOnlyList<SplitReason> Reasons)
{
    /// <summary>
    /// The suggestion as a split: each part's pages without the blank ones, unturned; a part that is
    /// blank throughout is left out. The result may be invalid (<see cref="SplitPlan.Validate"/>).
    /// </summary>
    public SplitPlan ToPlan()
    {
        var blank = BlankPages.ToHashSet();
        var parts = Parts
            .Select(part => part.Pages().Where(page => !blank.Contains(page)).Select(page => new PageStep(page, PageRotation.None, false)).ToList())
            .Where(steps => steps.Count > 0)
            .Select(steps => new PagePlan(steps))
            .ToList();
        return new SplitPlan(parts);
    }
}
