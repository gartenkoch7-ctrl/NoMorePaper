using System.Text.RegularExpressions;

namespace NoMorePaper.Application.Recognition;

/// <summary>
/// Shortens OCR text for the model (docs/kb/erkennung.md, "Kürzungsregel"): column padding and rules
/// are collapsed, then a long text keeps its head (letterhead, date, subject) and its tail (totals,
/// signature); the middle (terms, line items) is dropped.
/// </summary>
public static partial class RecognitionText
{
    /// <summary>Longest text sent whole.</summary>
    public const int MaxLength = 4000;

    /// <summary>Characters kept from the start of a longer text.</summary>
    public const int HeadLength = 3000;

    /// <summary>Characters kept from the end of a longer text.</summary>
    public const int TailLength = 1000;

    /// <summary>Line between head and tail, so the model knows text is missing.</summary>
    public const string Marker = "\n[… Mittelteil gekürzt …]\n";

    /// <summary>The text as the model gets it.</summary>
    public static string Shorten(string text)
    {
        var compact = Compact(text);
        return compact.Length <= MaxLength ? compact : HeadAndTail(compact);
    }

    /// <summary>Collapses the column padding of OCR output; it costs tokens and carries no meaning.</summary>
    public static string Compact(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var unified = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        var spaces = SpaceRuns().Replace(unified, "  ");
        var rules = RuleRuns().Replace(spaces, "---");
        return BlankLines().Replace(rules, "\n\n").Trim();
    }

    // Cut at line ends, so that no word or amount is split; a line end far from the limit is not used.
    private static string HeadAndTail(string text)
    {
        var headEnd = text.LastIndexOf('\n', HeadLength - 1);
        headEnd = headEnd < HeadLength / 2 ? HeadLength : headEnd;

        var tailStart = text.IndexOf('\n', text.Length - TailLength);
        tailStart = tailStart < 0 || tailStart > text.Length - (TailLength / 2) ? text.Length - TailLength : tailStart + 1;

        // Never between the two halves of a character outside the BMP (emoji).
        headEnd = char.IsLowSurrogate(text[headEnd]) ? headEnd - 1 : headEnd;
        tailStart = char.IsLowSurrogate(text[tailStart]) ? tailStart + 1 : tailStart;
        return text[..headEnd] + Marker + text[tailStart..];
    }

    [GeneratedRegex(@"[ \t]{3,}")]
    private static partial Regex SpaceRuns();

    [GeneratedRegex(@"[-_=.]{6,}")]
    private static partial Regex RuleRuns();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex BlankLines();
}
