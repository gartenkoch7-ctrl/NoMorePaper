using System.Globalization;
using System.Text.RegularExpressions;
using NoMorePaper.Application.Editing;
using NoMorePaper.Domain.Editing;

namespace NoMorePaper.Application.Separation;

/// <summary>
/// Where a scanned stack probably splits, read from the start of each page (M4b, stack separation):
/// probably blank pages, page counters ("Seite 1 von 3", "Page 2 of 2", "1/3") and salutations at the
/// top of a page. The local LLM adds sender changes (<see cref="SeparationCandidates.ToSuggestion"/>).
/// Pure logic.
/// </summary>
public static partial class SeparationHeuristics
{
    /// <summary>
    /// Whether the pages are a scanned stack worth separating: more than one page and none with a text
    /// layer of its own. A digital PDF (an invoice from a portal) is one document by itself.
    /// </summary>
    public static bool IsScannedStack(IReadOnlyList<PageInfo> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        return pages.Count > 1 && pages.All(page => !page.HadTextLayer);
    }

    /// <summary>The signs of each page; the pages are ordered by number, starting at 1 without gaps.</summary>
    public static SeparationCandidates Find(IReadOnlyList<PageInfo> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        var ordered = pages.OrderBy(page => page.Page).ToList();
        var blank = ordered.Where(page => page.ProbablyBlank).Select(page => page.Page).ToList();
        var starts = new Dictionary<int, SplitReason>();
        var continuations = new HashSet<int>();

        // An empty back side says nothing about where a document ends; a blank page only separates
        // when the stack is not a duplex scan of one-sided pages.
        var blankSeparates = !IsDuplexOfOneSidedPages(ordered);
        var afterBlank = false;
        foreach (var page in ordered)
        {
            if (page.ProbablyBlank)
            {
                afterBlank = blankSeparates;
                continue;
            }

            if (afterBlank)
            {
                starts.TryAdd(page.Page, SplitReason.Blank);
                afterBlank = false;
            }

            if (PageNumber(page.Head) is { } number)
            {
                if (number == 1)
                {
                    starts.TryAdd(page.Page, SplitReason.PageCounter);
                }
                else
                {
                    continuations.Add(page.Page);
                }
            }

            if (HasSalutation(page.Head))
            {
                starts.TryAdd(page.Page, SplitReason.Salutation);
            }
        }

        return new SeparationCandidates(ordered.Count, starts, continuations, blank);
    }

    /// <summary>
    /// The number of the page within its document from a counter at the top ("Seite 2 von 3",
    /// "Page 2 of 3", "Blatt 2/3", or "2/3" on a line of its own); <c>null</c> without a plausible counter.
    /// </summary>
    public static int? PageNumber(string head)
    {
        ArgumentNullException.ThrowIfNull(head);
        foreach (Match match in PageCounter().Matches(head))
        {
            var number = int.Parse(match.Groups["number"].Value, CultureInfo.InvariantCulture);
            var count = int.Parse(match.Groups["count"].Value, CultureInfo.InvariantCulture);
            if (number >= 1 && number <= count)
            {
                return number;
            }
        }

        return null;
    }

    /// <summary>Whether a line at the top of the page opens a letter ("Sehr geehrte", "Guten Tag", "Dear").</summary>
    public static bool HasSalutation(string head)
    {
        ArgumentNullException.ThrowIfNull(head);
        return Salutation().IsMatch(head);
    }

    // Every even page blank and no odd one: the fronts of one-sided pages, scanned on both sides.
    private static bool IsDuplexOfOneSidedPages(List<PageInfo> pages) =>
        pages.Count % 2 == 0 && pages.All(page => page.ProbablyBlank == (page.Page % 2 == 0));

    // A bare "2/3" counts only alone on its line, so dates (1/3/2026) and fractions in text do not.
    [GeneratedRegex(
        @"(?:\b(?:seite|page|blatt|s\.)\s*(?<number>\d{1,2})\s*(?:von|of|/)\s*(?<count>\d{1,2})\b)|(?:^[ \t]*(?<number>\d{1,2})[ \t]*/[ \t]*(?<count>\d{1,2})[ \t]*$)",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex PageCounter();

    [GeneratedRegex(
        @"^[ \t]*(?:sehr\s+geehrte|guten\s+tag\b|dear\b)",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex Salutation();
}
