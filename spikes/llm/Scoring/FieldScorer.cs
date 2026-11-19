using System.Globalization;
using System.Text.RegularExpressions;
using LlmSpike.Expected;
using LlmSpike.Text;

namespace LlmSpike.Scoring;

/// <summary>Compares one answer with the expected values. Pure rules, no model involved.</summary>
public static partial class FieldScorer
{
    private const int MaxTitleChars = 80;
    private const int MaxSummaryChars = 350;

    public static DocumentScore Score(ExpectedDocument expected, string content, Household household)
    {
        var answer = Recognition.Parse(content);
        if (answer is null)
        {
            return new DocumentScore(expected.Id, false, false, null, false, null, false, false, false, 0, 0, false, 0, false, false, ["kein gültiges JSON"]);
        }

        var misses = new List<string>();
        var correspondentMatch = Matches(answer.Correspondent, expected.Correspondent, expected.CorrespondentAliases);
        var typeMatch = Matches(answer.DocumentType, expected.DocumentType, expected.DocumentTypeAliases);
        var date = answer.DocumentDate == expected.Date;
        var amount = AmountMatches(answer.Amount, expected);
        // A missing currency on a missing amount is fine; with an amount the code must be right.
        var currency = answer.Amount is null || string.Equals(answer.Currency, expected.Currency ?? "EUR", StringComparison.Ordinal);
        var (recall, precision, hit, fromList) = Tags(answer.Tags, expected, household);
        var title = TitleOk(answer.Title, expected);
        var summary = SummaryOk(answer.Summary, expected);

        Note(misses, correspondentMatch, "Absender", answer.Correspondent, expected.Correspondent);
        Note(misses, typeMatch, "Art", answer.DocumentType, expected.DocumentType);
        Note(misses, date, "Datum", answer.DocumentDate, expected.Date);
        Note(misses, amount && currency, "Betrag", Money(answer.Amount, answer.Currency), Money(expected.Amount, expected.Currency));
        Note(misses, hit, "Tags", string.Join(", ", answer.Tags), string.Join(", ", expected.Tags));
        Note(misses, title, "Titel", answer.Title, expected.Title);
        Note(misses, summary, "Zusammenfassung", answer.Summary, null);

        return new DocumentScore(
            expected.Id, true,
            correspondentMatch, FromList(answer.Correspondent, expected.Correspondent, household.Correspondents),
            typeMatch, FromList(answer.DocumentType, expected.DocumentType, household.DocumentTypes),
            date, amount, amount && currency, recall, precision, hit, fromList, title, summary, misses);
    }

    private static bool? FromList(string? actual, string expected, IReadOnlyList<string> list) =>
        list.Contains(expected, StringComparer.Ordinal) ? string.Equals(actual, expected, StringComparison.Ordinal) : null;

    private static bool Matches(string? actual, string expected, IReadOnlyList<string> aliases) =>
        NameNormalizer.Same(actual, expected) || aliases.Any(alias => NameNormalizer.Same(actual, alias));

    private static bool AmountMatches(decimal? actual, ExpectedDocument expected)
    {
        static decimal? Round(decimal? value) => value is null ? null : Math.Round(value.Value, 2);
        return Round(actual) == Round(expected.Amount) || expected.AmountAlternatives.Any(alternative => Round(alternative) == Round(actual));
    }

    /// <summary>
    /// Recall: share of expected tags that were given. Precision: share of given tags that are expected
    /// or accepted. Hit: at least one good tag. FromList: share of given tags that already exist.
    /// </summary>
    private static (double Recall, double Precision, bool Hit, double FromList) Tags(IReadOnlyList<string> actual, ExpectedDocument expected, Household household)
    {
        var given = actual.Select(NameNormalizer.Normalize).Where(tag => tag.Length > 0).ToHashSet(StringComparer.Ordinal);
        var core = expected.Tags
            .Select(entry => entry.Split('|').Select(NameNormalizer.Normalize).ToList())
            .ToList();
        var found = core.Count(alternatives => alternatives.Any(given.Contains));
        var good = core.SelectMany(alternatives => alternatives)
            .Concat(expected.TagsAccepted.Select(NameNormalizer.Normalize))
            .ToHashSet(StringComparer.Ordinal);
        var known = household.Tags.Select(NameNormalizer.Normalize).ToHashSet(StringComparer.Ordinal);
        double Share(HashSet<string> set) => given.Count == 0 ? 0 : (double)given.Count(set.Contains) / given.Count;
        return (core.Count == 0 ? 1 : (double)found / core.Count, Share(good), given.Any(good.Contains), Share(known));
    }

    /// <summary>Rule: present, short, names the subject, and is German.</summary>
    private static bool TitleOk(string? title, ExpectedDocument expected) =>
        !string.IsNullOrWhiteSpace(title)
        && title.Length <= MaxTitleChars
        && ContainsAny(title, expected.TitleKeywords)
        && !EnglishTitleWords().IsMatch(title);

    /// <summary>Rule: present, one to two sentences (three tolerated), names a key fact, and is German.</summary>
    private static bool SummaryOk(string? summary, ExpectedDocument expected) =>
        !string.IsNullOrWhiteSpace(summary)
        && summary.Length <= MaxSummaryChars
        && SentenceEnds().Count(Abbreviations().Replace(summary, "$1")) <= 3
        && ContainsAny(summary, expected.SummaryKeywords)
        && GermanWords().Count(summary) >= 2;

    private static bool ContainsAny(string text, IReadOnlyList<string> keywords)
    {
        var folded = NameNormalizer.Normalize(text);
        return keywords.Any(keyword =>
            text.Contains(keyword, StringComparison.OrdinalIgnoreCase)
            || (NameNormalizer.Normalize(keyword) is { Length: > 0 } key && folded.Contains(key, StringComparison.Ordinal)));
    }

    private static void Note(List<string> misses, bool ok, string field, string? actual, string? expected)
    {
        if (!ok)
        {
            misses.Add(expected is null ? $"{field}: „{actual ?? "null"}“" : $"{field}: „{actual ?? "null"}“ statt „{expected}“");
        }
    }

    private static string? Money(decimal? amount, string? currency) =>
        amount is null ? null : string.Create(CultureInfo.InvariantCulture, $"{amount:0.00} {currency}");

    [GeneratedRegex(@"\b(invoice|booking|confirmation|statement|receipt)\b", RegexOptions.IgnoreCase)]
    private static partial Regex EnglishTitleWords();

    // "inkl. Ölwechsel" is not the end of a sentence.
    [GeneratedRegex(@"\b(inkl|zzgl|ca|bzw|ggf|evtl|max|mind|Nr|Dr|Str|Abs|St|z|B|u|a|d|h)\.")]
    private static partial Regex Abbreviations();

    // A full stop inside a date or amount (15.11.2026, 2.487,36) is followed by a digit, not by a space.
    [GeneratedRegex(@"[.!?](\s+(?=[A-ZÄÖÜ])|\s*$)")]
    private static partial Regex SentenceEnds();

    [GeneratedRegex(@"\b(der|die|das|und|für|über|von|mit|ist|wird|zum|zur|bis|eine|einen|des|dem|den|am|im|auf)\b", RegexOptions.IgnoreCase)]
    private static partial Regex GermanWords();
}
