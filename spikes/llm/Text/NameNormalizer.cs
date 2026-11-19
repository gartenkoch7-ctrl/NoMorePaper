using System.Globalization;
using System.Text;

namespace LlmSpike.Text;

/// <summary>
/// Normalised form for comparing list names: lower case, no accents, no punctuation, no legal-form
/// suffixes and no title additions such as "med.". Mirrors the rule planned for
/// <c>ClassificationName.Normalize</c> (M3, package P0).
/// </summary>
public static class NameNormalizer
{
    private static readonly HashSet<string> IgnoredWords = new(StringComparer.Ordinal)
    {
        "gmbh", "ag", "ev", "kg", "co", "mbh", "se", "ltd", "eg", "ek", "ohg", "ug", "inc", "plc", "med", "dent",
    };

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var c in value.Replace("ß", "ss", StringComparison.Ordinal).Normalize(NormalizationForm.FormD))
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category == UnicodeCategory.NonSpacingMark || c == '.')
            {
                // "e.V." must become "ev", so dots vanish instead of splitting the word.
                continue;
            }

            builder.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        }

        var words = builder.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(word => !IgnoredWords.Contains(word));
        return string.Join(' ', words);
    }

    public static bool Same(string? left, string? right)
    {
        var a = Normalize(left);
        return a.Length > 0 && a == Normalize(right);
    }
}
