using NoMorePaper.Application.Classification;

namespace NoMorePaper.Application.Recognition;

/// <summary>
/// Which list entries go into the prompt (docs/kb/erkennung.md, "Vorgaben für den Worker"): all document
/// types, but correspondents and tags only up to a limit, because the prompt has to fit the context.
/// </summary>
public static class RecognitionLists
{
    /// <summary>Most correspondents in the prompt; those named in the text always come first.</summary>
    public const int MaxCorrespondents = 100;

    /// <summary>Most tags in the prompt.</summary>
    public const int MaxTags = 50;

    /// <summary>The entries named in the text as whole words, the earliest named first.</summary>
    public static IReadOnlyList<EntrySummary> Mentioned(NormalizedText text, IEnumerable<EntrySummary> entries)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(entries);
        return entries
            .Select(entry => (Entry: entry, Position: text.PositionOf(entry.Name)))
            .Where(found => found.Position >= 0)
            .OrderBy(found => found.Position)
            .ThenBy(found => found.Entry.Name, StringComparer.Ordinal)
            .Select(found => found.Entry)
            .ToList();
    }

    /// <summary>
    /// The names of all entries in their order when they fit into <paramref name="max"/>; otherwise
    /// <paramref name="preferred"/> first, then the others up to the limit.
    /// </summary>
    /// <remarks>
    /// The order stays the same between documents while the list fits: the names are part of the system
    /// text, whose evaluation Ollama reuses only while it is unchanged (about 20 s per document).
    /// </remarks>
    public static IReadOnlyList<string> Names(IReadOnlyCollection<EntrySummary> entries, IEnumerable<EntrySummary> preferred, int max)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(preferred);
        var chosen = entries.Count <= max ? entries : preferred.Concat(entries).DistinctBy(entry => entry.Id).Take(max);
        return [.. chosen.Select(entry => entry.Name)];
    }
}
