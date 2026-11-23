using System.Globalization;
using System.Text;

namespace NoMorePaper.Infrastructure.Editing;

/// <summary>Reads what the poppler tools wrote: page texts, font lists and grey images. Pure logic.</summary>
public static class PopplerOutput
{
    // Darker than half grey counts as ink.
    private const int InkThreshold = 128;

    /// <summary>The texts of the pages; <c>pdftotext</c> ends every page with a form feed.</summary>
    public static IReadOnlyList<string> SplitPages(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var parts = text.Split('\f');

        // The part after the last form feed is no page.
        return parts.Length <= 1 ? [] : parts[..^1];
    }

    /// <summary>The start of a page's text with whitespace collapsed, at most <paramref name="maxLength"/> characters.</summary>
    public static string Head(string pageText, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(pageText);
        var head = new StringBuilder(maxLength);
        var space = false;
        foreach (var c in pageText)
        {
            if (head.Length >= maxLength)
            {
                break;
            }

            if (char.IsWhiteSpace(c) || c == '\0')
            {
                space = head.Length > 0;
                continue;
            }

            if (space && head.Length < maxLength - 1)
            {
                head.Append(' ');
            }

            space = false;
            head.Append(c);
        }

        // Never end in half a surrogate pair.
        return head.Length > 0 && char.IsHighSurrogate(head[^1]) ? head.ToString(0, head.Length - 1) : head.ToString();
    }

    /// <summary>How many characters of the page text are not whitespace.</summary>
    public static int VisibleCharacters(string pageText) => pageText.Count(c => !char.IsWhiteSpace(c) && c != '\0');

    /// <summary>Whether a <c>pdffonts</c> listing names a font: two header lines, then one line per font.</summary>
    public static bool HasFonts(string listing)
    {
        ArgumentNullException.ThrowIfNull(listing);
        return listing.Split('\n').Skip(2).Any(line => !string.IsNullOrWhiteSpace(line));
    }

    /// <summary>The share of dark pixels in an 8-bit binary PGM (<c>pdftoppm -gray</c>); 0 for an empty image.</summary>
    /// <exception cref="FormatException">Not an 8-bit binary PGM.</exception>
    public static double DarkShare(ReadOnlySpan<byte> pgm)
    {
        var position = 0;
        if (Token(pgm, ref position) != "P5")
        {
            throw new FormatException("Not a binary PGM.");
        }

        var width = Number(pgm, ref position);
        var height = Number(pgm, ref position);
        var maxValue = Number(pgm, ref position);
        if (maxValue is < 1 or > 255)
        {
            throw new FormatException("Only 8-bit PGM images are read.");
        }

        // Exactly one whitespace byte separates the header from the pixels.
        var pixels = pgm[(position + 1)..];
        var count = (long)width * height;
        if (count == 0)
        {
            return 0;
        }

        if (pixels.Length < count)
        {
            throw new FormatException("The PGM image is cut off.");
        }

        var threshold = InkThreshold * maxValue / 255;
        var dark = 0L;
        foreach (var pixel in pixels[..(int)count])
        {
            dark += pixel < threshold ? 1 : 0;
        }

        return (double)dark / count;
    }

    private static int Number(ReadOnlySpan<byte> pgm, ref int position) =>
        int.TryParse(Token(pgm, ref position), NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new FormatException("Bad PGM header.");

    // A header token; comments run from '#' to the end of the line.
    private static string Token(ReadOnlySpan<byte> pgm, ref int position)
    {
        while (position < pgm.Length && (IsSpace(pgm[position]) || pgm[position] == (byte)'#'))
        {
            if (pgm[position] == (byte)'#')
            {
                while (position < pgm.Length && pgm[position] != (byte)'\n')
                {
                    position++;
                }
            }
            else
            {
                position++;
            }
        }

        var start = position;
        while (position < pgm.Length && !IsSpace(pgm[position]))
        {
            position++;
        }

        return Encoding.ASCII.GetString(pgm[start..position]);
    }

    private static bool IsSpace(byte value) => value is (byte)' ' or (byte)'\t' or (byte)'\n' or (byte)'\r';
}
