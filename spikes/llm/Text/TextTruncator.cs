using System.Text.RegularExpressions;

namespace LlmSpike.Text;

/// <summary>
/// Shortens OCR text for the model: the head carries letterhead, date and subject, the tail carries
/// totals and signatures. The middle (terms and conditions, line items) is dropped.
/// </summary>
public static partial class TextTruncator
{
    public const string Marker = "\n[… Mittelteil gekürzt …]\n";

    /// <summary>Collapses the column padding of OCR output; it costs tokens and carries no meaning.</summary>
    public static string Compact(string text)
    {
        var unified = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        var spaces = SpaceRuns().Replace(unified, "  ");
        var rules = RuleRuns().Replace(spaces, "---");
        return BlankLines().Replace(rules, "\n\n").Trim();
    }

    /// <summary>Keeps <paramref name="head"/> and <paramref name="tail"/> characters; 0 for head means no limit.</summary>
    public static string HeadAndTail(string text, int head, int tail)
    {
        if (head <= 0 || text.Length <= head + tail)
        {
            return text;
        }

        // Cut at line ends so that no word or amount is split.
        var headEnd = text.LastIndexOf('\n', head - 1);
        headEnd = headEnd < head / 2 ? head : headEnd;
        if (tail <= 0)
        {
            return text[..headEnd] + Marker.TrimEnd('\n');
        }

        var tailStart = text.IndexOf('\n', text.Length - tail);
        tailStart = tailStart < 0 || tailStart > text.Length - (tail / 2) ? text.Length - tail : tailStart + 1;
        return text[..headEnd] + Marker + text[tailStart..];
    }

    [GeneratedRegex(@"[ \t]{3,}")]
    private static partial Regex SpaceRuns();

    [GeneratedRegex(@"[-_=.]{6,}")]
    private static partial Regex RuleRuns();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex BlankLines();
}
