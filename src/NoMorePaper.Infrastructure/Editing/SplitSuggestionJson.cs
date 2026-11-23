using System.Text.Json;
using System.Text.Json.Serialization;
using NoMorePaper.Domain.Editing;

namespace NoMorePaper.Infrastructure.Editing;

/// <summary>
/// The JSON of <c>documents.split_suggestion</c>: <c>{"parts":[[1,2],[3,3]],"blank":[5],"reasons":["Blank"]}</c>.
/// Kept apart from the domain type, so renaming a property never breaks stored suggestions. Pure logic.
/// </summary>
internal static class SplitSuggestionJson
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        Converters = { new JsonStringEnumConverter<SplitReason>() },
    };

    public static string Write(SplitSuggestion suggestion)
    {
        ArgumentNullException.ThrowIfNull(suggestion);
        var stored = new Stored(
            [.. suggestion.Parts.Select(part => new[] { part.First, part.Last })],
            [.. suggestion.BlankPages],
            [.. suggestion.Reasons]);
        return JsonSerializer.Serialize(stored, s_options);
    }

    public static SplitSuggestion Read(string json)
    {
        var stored = JsonSerializer.Deserialize<Stored>(json, s_options)
            ?? throw new JsonException("A split suggestion is no JSON null.");
        return new SplitSuggestion(
            [.. stored.Parts.Select(part => new PageRange(part[0], part[1]))],
            stored.Blank,
            stored.Reasons);
    }

    private sealed record Stored(
        [property: JsonPropertyName("parts")] int[][] Parts,
        [property: JsonPropertyName("blank")] int[] Blank,
        [property: JsonPropertyName("reasons")] SplitReason[] Reasons);
}
