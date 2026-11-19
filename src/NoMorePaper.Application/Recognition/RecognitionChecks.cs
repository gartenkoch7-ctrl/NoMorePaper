using NoMorePaper.Application.Classification;
using NoMorePaper.Domain.Classification;

namespace NoMorePaper.Application.Recognition;

/// <summary>
/// Checks the answer of the model against the OCR text (docs/kb/erkennung.md, "Prüfungen nach der
/// Antwort"); each check would have caught an error seen in the spike. A value that fails is dropped,
/// the others are kept. The model's answer is untrusted data: it can be wrong, or steered by the text.
/// </summary>
public static class RecognitionChecks
{
    /// <summary>Longest title kept; the prompt asks for 60 characters.</summary>
    public const int MaxTitleLength = 80;

    /// <summary>Longest summary kept; the prompt asks for one or two sentences.</summary>
    public const int MaxSummaryLength = 500;

    /// <summary>
    /// Words of a listed correspondent's name this long must all occur in the text; a name without such
    /// words must occur as a whole.
    /// </summary>
    public const int MinCheckedWordLetters = 4;

    /// <summary>Name of a field in <see cref="CheckedRecognition.Rejected"/>; fixed words, safe to log.</summary>
    public const string Correspondent = "correspondent";

    public const string DocumentDate = "date";

    public const string Amount = "amount";

    public const string Tags = "tags";

    /// <param name="result">The answer of the model.</param>
    /// <param name="text">The full OCR text of the document.</param>
    /// <param name="correspondents">The correspondents the owner may see.</param>
    /// <param name="today">
    /// Today's date in UTC; a document date more than a day after it is dropped (the day of tolerance
    /// covers time zones ahead of UTC).
    /// </param>
    public static CheckedRecognition Apply(
        RecognitionResult result, string text, IReadOnlyCollection<EntrySummary> correspondents, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(correspondents);
        var rejected = new List<string>();
        var normalized = new NormalizedText(text);

        var correspondent = Clean(result.Correspondent);
        if (correspondent is not null && IsListed(correspondent, correspondents)
            && !normalized.Mentions(correspondent, MinCheckedWordLetters))
        {
            // A listed entry the text does not name is worse than a new name: it looks right.
            rejected.Add(Correspondent);
            correspondent = null;
        }

        var date = result.DocumentDate;
        if (date is { } value && (value > today.AddDays(1) || !DateNotation.YearAppearsIn(value.Year, text)))
        {
            rejected.Add(DocumentDate);
            date = null;
        }

        var amount = result.Amount;
        if (amount is not null && !AmountNotation.AppearsIn(amount.Value, text))
        {
            rejected.Add(Amount);
            amount = null;
        }

        // The answered correspondent, even if dropped above: a made-up sender must not come back as a tag.
        var documentType = Clean(result.DocumentType);
        var tags = DistinctTags(result.Tags, Clean(result.Correspondent), documentType);
        if (tags.Count < result.Tags.Count(tag => Clean(tag) is not null))
        {
            rejected.Add(Tags);
        }

        var checkedResult = new RecognitionResult(
            correspondent, documentType, date, amount, tags, Cut(result.Title, MaxTitleLength), Cut(result.Summary, MaxSummaryLength));
        return new CheckedRecognition(checkedResult, rejected);
    }

    private static bool IsListed(string name, IReadOnlyCollection<EntrySummary> entries)
    {
        var normalized = ClassificationName.Normalize(name);
        return entries.Any(entry => ClassificationName.Normalize(entry.Name) == normalized);
    }

    // A tag that repeats the type or the correspondent says nothing new; equal tags count once.
    private static List<string> DistinctTags(IReadOnlyList<string> tags, string? correspondent, string? documentType)
    {
        var excluded = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in new[] { correspondent, documentType })
        {
            if (name is not null)
            {
                excluded.Add(ClassificationName.Normalize(name));
            }
        }

        var kept = new List<string>();
        foreach (var tag in tags.Select(Clean).OfType<string>())
        {
            if (excluded.Add(ClassificationName.Normalize(tag)))
            {
                kept.Add(tag);
            }
        }

        return kept;
    }

    // Null for what the lists would refuse, so that matching a name to an entry cannot fail.
    private static string? Clean(string? value) => EntryNames.TryClean(value, out var cleaned) ? cleaned : null;

    private static string? Cut(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length <= maxLength)
        {
            return trimmed;
        }

        // Never between the two halves of a character outside the BMP (emoji): the database refuses a half.
        var end = char.IsHighSurrogate(trimmed[maxLength - 1]) ? maxLength - 1 : maxLength;
        return trimmed[..end].TrimEnd();
    }
}
