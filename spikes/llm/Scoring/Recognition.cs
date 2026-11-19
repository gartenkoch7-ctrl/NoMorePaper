using System.Text.Json;

namespace LlmSpike.Scoring;

/// <summary>The model's answer, parsed leniently so that a wrong type counts as a miss, not a crash.</summary>
public sealed record Recognition(
    string? Correspondent,
    string? DocumentType,
    string? DocumentDate,
    decimal? Amount,
    string? Currency,
    IReadOnlyList<string> Tags,
    string? Title,
    string? Summary)
{
    public static Recognition? Parse(string content)
    {
        try
        {
            using var json = JsonDocument.Parse(content);
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return new Recognition(
                Text(root, "correspondent"),
                Text(root, "documentType"),
                Text(root, "documentDate"),
                root.TryGetProperty("amount", out var amount) && amount.ValueKind == JsonValueKind.Number && amount.TryGetDecimal(out var number) ? number : null,
                Text(root, "currency"),
                // The discarded third prompt version split the tags into "tags" and "newTags"; they are scored together.
                [.. Strings(root, "tags"), .. Strings(root, "newTags")],
                Text(root, "title"),
                Text(root, "summary"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static List<string> Strings(JsonElement root, string name) =>
        root.TryGetProperty(name, out var values) && values.ValueKind == JsonValueKind.Array
            ? values.EnumerateArray().Where(v => v.ValueKind == JsonValueKind.String).Select(v => v.GetString()!).ToList()
            : [];

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
