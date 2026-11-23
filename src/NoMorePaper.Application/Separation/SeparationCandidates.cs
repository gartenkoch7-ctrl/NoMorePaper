using NoMorePaper.Domain.Editing;

namespace NoMorePaper.Application.Separation;

/// <summary>
/// What <see cref="SeparationHeuristics.Find"/> saw in a stack, before the local LLM adds its view.
/// Pure logic.
/// </summary>
/// <param name="PageCount">Pages of the stack, numbered from 1.</param>
/// <param name="Starts">Pages that probably start a new document, with the first sign seen.</param>
/// <param name="Continuations">Pages whose counter says they continue a document ("Seite 2 von 3").</param>
/// <param name="BlankPages">Probably blank pages, in order.</param>
public sealed record SeparationCandidates(
    int PageCount,
    IReadOnlyDictionary<int, SplitReason> Starts,
    IReadOnlySet<int> Continuations,
    IReadOnlyList<int> BlankPages)
{
    /// <summary>
    /// The suggestion from these signs and the pages where the local LLM saw a new document start. A
    /// counter that says a page continues its document overrules every other sign; a start on a blank
    /// page moves to the next page with content. Blank pages stay inside the parts and are listed apart.
    /// </summary>
    /// <param name="senderChanges">Pages where the LLM saw another document start; empty without the LLM.</param>
    /// <returns>The suggestion, or <c>null</c> when the stack is one document (or blank throughout).</returns>
    public SplitSuggestion? ToSuggestion(IReadOnlyList<int> senderChanges)
    {
        ArgumentNullException.ThrowIfNull(senderChanges);
        var blank = BlankPages.ToHashSet();
        var first = Enumerable.Range(1, PageCount).FirstOrDefault(page => !blank.Contains(page));
        if (first == 0)
        {
            return null;
        }

        var signs = Starts.Select(start => (start.Key, start.Value))
            .Concat(senderChanges.Select(page => (Key: page, Value: SplitReason.SenderChange)));
        var starts = new SortedDictionary<int, SplitReason>();
        foreach (var (page, reason) in signs)
        {
            var start = page;
            while (blank.Contains(start))
            {
                start++;
            }

            if (start > first && start <= PageCount && !Continuations.Contains(start))
            {
                starts.TryAdd(start, reason);
            }
        }

        if (starts.Count == 0)
        {
            return null;
        }

        // Each part starts at a page with content (the first part at page 1), so none is blank throughout.
        var bounds = starts.Keys.Prepend(1).Append(PageCount + 1).ToList();
        var parts = bounds.Zip(bounds.Skip(1), (from, next) => new PageRange(from, next - 1)).ToList();
        var reasons = starts.Values.Distinct().Order().ToList();
        return new SplitSuggestion(parts, [.. BlankPages], reasons);
    }
}
