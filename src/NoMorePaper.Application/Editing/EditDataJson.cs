using System.Text.Json;
using System.Text.Json.Serialization;

namespace NoMorePaper.Application.Editing;

/// <summary>
/// <see cref="EditData"/> as the JSON of <c>document_edits.data</c>: camel case, enums as names, empty
/// parts left out. Pure logic.
/// </summary>
public static class EditDataJson
{
    private static readonly JsonSerializerOptions s_options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string ToJson(EditData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return JsonSerializer.Serialize(data, s_options);
    }

    /// <returns>The plan, or <c>null</c> when the JSON is no plan of <paramref name="kind"/> (then the edit cannot be carried out).</returns>
    public static EditData? Parse(string json, EditKind kind)
    {
        EditData? data;
        try
        {
            data = JsonSerializer.Deserialize<EditData>(json, s_options);
        }
        catch (JsonException)
        {
            return null;
        }

        return data is not null && data.EditId.Version == 7 && Fits(data, kind) ? data : null;
    }

    private static bool Fits(EditData data, EditKind kind) => kind switch
    {
        EditKind.Pages => data.Steps is { Count: > 0 } steps && steps.All(step => step is not null),
        EditKind.Append => data.AppendedId is { } appended && appended != Guid.Empty,
        EditKind.Split => data.Parts is { Count: > 1 } parts && parts.All(part => part is { Count: > 0 } && part.All(step => step is not null)),
        EditKind.Rebuild => true,
        _ => false,
    };
}
