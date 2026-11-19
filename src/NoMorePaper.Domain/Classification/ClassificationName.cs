using System.Globalization;
using System.Text;

namespace NoMorePaper.Domain.Classification;

/// <summary>
/// Rules for the names of list entries: what a valid name is, and the comparison key under which
/// "Stadtwerke München GmbH" and "stadtwerke-munchen" are the same entry.
/// </summary>
public static class ClassificationName
{
    /// <summary>Longest name kept.</summary>
    public const int MaxLength = 200;

    // Legal-form words, removed only from the end ("AG Leipzig" stays): "GmbH & Co. KG" loses all
    // three, "UG (haftungsbeschränkt)" both. "e.V." and "eG" arrive here without their dots.
    private static readonly HashSet<string> s_legalForms = new(StringComparer.Ordinal)
    {
        "gmbh", "mbh", "ag", "kg", "kgaa", "ohg", "gbr", "ug", "haftungsbeschrankt", "ev", "eg", "se", "co",
        "ltd", "inc", "llc", "plc",
    };

    /// <summary>Returns the trimmed name.</summary>
    /// <exception cref="ArgumentException">
    /// The name is empty, longer than <see cref="MaxLength"/> or has neither letters nor digits.
    /// </exception>
    public static string Clean(string name, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, parameterName);
        var trimmed = name.Trim();
        if (trimmed.Length > MaxLength)
        {
            throw new ArgumentException($"At most {MaxLength} characters.", parameterName);
        }

        if (Normalize(trimmed).Length == 0)
        {
            throw new ArgumentException("A name needs letters or digits.", parameterName);
        }

        return trimmed;
    }

    /// <summary>
    /// Comparison key: lower case, without accents, punctuation and trailing legal forms, words
    /// separated by one space. A name made only of legal forms keeps its last word ("AG" stays "ag").
    /// </summary>
    public static string Normalize(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var words = Words(name);
        var end = words.Length;
        while (end > 1 && s_legalForms.Contains(words[end - 1]))
        {
            end--;
        }

        return string.Join(' ', words, 0, end);
    }

    private static string[] Words(string name)
    {
        // Compatibility decomposition splits "é" into "e" + accent and folds ligatures and full-width letters.
        var decomposed = name.Normalize(NormalizationForm.FormKD);
        var builder = new StringBuilder(decomposed.Length);
        // Separators become spaces ("Stadtwerke-München"); accents, other punctuation and symbols are
        // dropped, so "E.ON" and "EON" are the same name.
        foreach (var c in decomposed)
        {
            if (char.IsLetterOrDigit(c))
            {
                AppendLower(builder, c);
            }
            else if (IsSeparator(c))
            {
                builder.Append(' ');
            }
        }

        return builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    private static bool IsSeparator(char c) =>
        char.IsWhiteSpace(c)
        || c == '/'
        || CharUnicodeInfo.GetUnicodeCategory(c) is UnicodeCategory.DashPunctuation or UnicodeCategory.ConnectorPunctuation;

    private static void AppendLower(StringBuilder builder, char c)
    {
        var lower = char.ToLowerInvariant(c);
        if (lower == 'ß')
        {
            builder.Append("ss");
        }
        else
        {
            builder.Append(lower);
        }
    }
}
