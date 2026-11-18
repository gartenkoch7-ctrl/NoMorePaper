using System.Globalization;
using System.Text.Json;

namespace ICloudSpike.Http;

/// <summary>
/// Tolerant readers for Apple's JSON. The format is undocumented and shifts over time, so a missing
/// or differently typed field yields null instead of an exception.
/// </summary>
internal static class JsonReading
{
    public static JsonElement? Child(this JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) ? value : null;

    public static JsonElement? Child(this JsonElement? element, string name) => element?.Child(name);

    /// <summary>String value; numbers are accepted because Apple sends ids and codes in both forms.</summary>
    public static string? Text(this JsonElement? element) => element?.ValueKind switch
    {
        JsonValueKind.String => element.Value.GetString(),
        JsonValueKind.Number => element.Value.GetRawText(),
        _ => null,
    };

    public static bool? Flag(this JsonElement? element) => element?.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null,
    };

    public static long? Number(this JsonElement? element) => element?.ValueKind switch
    {
        JsonValueKind.Number when element.Value.TryGetInt64(out var number) => number,
        JsonValueKind.String when long.TryParse(
            element.Value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
        _ => null,
    };

    public static IEnumerable<JsonElement> Items(this JsonElement? element) =>
        element?.ValueKind == JsonValueKind.Array ? element.Value.EnumerateArray() : [];
}
