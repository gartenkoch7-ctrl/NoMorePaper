namespace NoMorePaper.Application.Editing;

/// <summary>
/// Whether the pages of an edited document had text before they were scanned. The edited version is made
/// from the archive, whose pages all carry the OCR text, so the analysis would find a text layer on every
/// page; what counts is what the page it came from had. Pure logic.
/// </summary>
public static class PageTextLayers
{
    /// <summary>The text layer each new page had where it came from.</summary>
    /// <param name="steps">The new pages in order, each with its page of the source.</param>
    /// <param name="source">The stored page information of the source; may be empty for documents from before M4b.</param>
    /// <returns>Per new page: known, <c>false</c> for a replaced page (an image now), or <c>null</c> when unknown.</returns>
    public static IReadOnlyList<bool?> Of(IEnumerable<(int SourcePage, bool Replaced)> steps, IReadOnlyList<PageInfo> source)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(source);
        return [.. steps.Select(step => step.Replaced ? false : source.FirstOrDefault(page => page.Page == step.SourcePage)?.HadTextLayer)];
    }

    /// <summary>The analyzed pages with the text layers of where they came from; unknown ones keep the analysis.</summary>
    public static IReadOnlyList<PageInfo> Apply(IReadOnlyList<PageInfo> analyzed, IReadOnlyList<bool?> layers)
    {
        ArgumentNullException.ThrowIfNull(analyzed);
        ArgumentNullException.ThrowIfNull(layers);
        return [.. analyzed.Select(page => page.Page <= layers.Count && layers[page.Page - 1] is { } had ? page with { HadTextLayer = had } : page)];
    }
}
