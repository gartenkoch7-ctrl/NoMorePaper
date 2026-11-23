namespace NoMorePaper.Application.Documents;

/// <summary>Shortens a summary or recognized text to a one-line excerpt for the document list. Pure logic.</summary>
public static class DocumentExcerpt
{
    public const int MaxLength = 160;

    private const int MinCut = 80;

    public static string? From(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var flat = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (flat.Length <= MaxLength)
        {
            return flat;
        }

        // Cut at a word boundary when one is far enough in, so the excerpt does not end in half a word.
        var space = flat.LastIndexOf(' ', MaxLength - 1, MaxLength - MinCut);
        var cut = space >= MinCut ? space : MaxLength;
        return flat[..cut].TrimEnd() + "…";
    }
}
