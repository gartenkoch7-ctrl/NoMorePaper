namespace NoMorePaper.Application.Editing;

/// <summary>What a planned edit does (<see cref="DocumentEdit"/>).</summary>
public enum EditKind
{
    /// <summary>Turn, delete, reorder and replace pages (a <see cref="Domain.Editing.PagePlan"/>).</summary>
    Pages,

    /// <summary>Append the pages of another document.</summary>
    Append,

    /// <summary>Split into several new documents (a <see cref="Domain.Editing.SplitPlan"/>).</summary>
    Split,

    /// <summary>Make the archive anew from the current source, e.g. after the edited version was dropped.</summary>
    Rebuild,
}
