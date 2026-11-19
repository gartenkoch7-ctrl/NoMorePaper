using System.Globalization;
using System.Text.Json;
using NoMorePaper.Application.Recognition;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Infrastructure.Recognition;

/// <summary>
/// Reads the model's JSON answer (<see cref="OllamaSchema"/>) into a <see cref="RecognitionResult"/>. The
/// grammar fixes the form, not the values: a value of the wrong type or out of range becomes empty.
/// </summary>
public static class OllamaAnswer
{
    /// <summary>Currency when the answer has an amount but none.</summary>
    public const string DefaultCurrency = "EUR";

    /// <exception cref="RecognitionException">The answer is no JSON object.</exception>
    public static RecognitionResult Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        try
        {
            using var json = JsonDocument.Parse(content);
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new RecognitionException("answer is no JSON object");
            }

            return new RecognitionResult(
                Text(root, "correspondent"),
                Text(root, "documentType"),
                Date(Text(root, "documentDate")),
                Amount(root),
                Tags(root),
                Text(root, "title"),
                Text(root, "summary"));
        }
        catch (JsonException exception)
        {
            throw new RecognitionException("answer is no JSON", exception);
        }
    }

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { } text
            && !string.IsNullOrWhiteSpace(text)
            ? text.Trim()
            : null;

    private static DateOnly? Date(string? text) =>
        DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;

    private static Money? Amount(JsonElement root)
    {
        if (!root.TryGetProperty("amount", out var amount) || amount.ValueKind != JsonValueKind.Number
            || !amount.TryGetDecimal(out var value))
        {
            return null;
        }

        value = decimal.Round(value, Money.MaxDecimalPlaces);
        if (Math.Abs(value) > Money.MaxAbsoluteValue)
        {
            return null;
        }

        var currency = Text(root, "currency");
        return new Money(value, currency is { Length: 3 } && currency.All(char.IsAsciiLetterUpper) ? currency : DefaultCurrency);
    }

    private static List<string> Tags(JsonElement root)
    {
        if (!root.TryGetProperty("tags", out var tags) || tags.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return [.. tags.EnumerateArray()
            .Where(tag => tag.ValueKind == JsonValueKind.String)
            .Select(tag => tag.GetString()?.Trim())
            .OfType<string>()
            .Where(tag => tag.Length > 0)
            .Take(OllamaSchema.MaxTags)];
    }
}
