using NoMorePaper.Domain.Classification;

namespace NoMorePaper.Application.Recognition;

/// <summary>
/// An OCR text in the comparison form of list names (<see cref="ClassificationName.Normalize"/>: lower
/// case, without accents and punctuation), to find names in it despite case, umlauts and line breaks.
/// </summary>
public sealed class NormalizedText
{
    // Spaces around it, so that a whole name can be searched as " name ".
    private readonly string _padded;

    public NormalizedText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        _padded = " " + ClassificationName.Normalize(text) + " ";
    }

    /// <summary>Where <paramref name="name"/> first occurs as whole words, or -1; -1 also for a name without letters or digits.</summary>
    public int PositionOf(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var normalized = ClassificationName.Normalize(name);
        return normalized.Length == 0 ? -1 : _padded.IndexOf(" " + normalized + " ", StringComparison.Ordinal);
    }

    /// <summary>
    /// Whether the text names <paramref name="name"/>: every word of it with at least
    /// <paramref name="minLetters"/> letters occurs, also inside a longer word (OCR often loses spaces).
    /// A name without such words (<c>AOK</c>) must occur as a whole.
    /// </summary>
    public bool Mentions(string name, int minLetters)
    {
        ArgumentNullException.ThrowIfNull(name);
        var longWords = ClassificationName.Normalize(name)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(word => word.Count(char.IsLetter) >= minLetters)
            .ToList();
        return longWords.Count == 0
            ? PositionOf(name) >= 0
            : longWords.TrueForAll(word => _padded.Contains(word, StringComparison.Ordinal));
    }
}
