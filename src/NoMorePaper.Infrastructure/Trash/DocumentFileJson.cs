using System.Text.Json;
using System.Text.Json.Serialization;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Infrastructure.Trash;

/// <summary>
/// The JSON of <c>trash_items.file</c>: the metadata of a file in the trash, so it can be put back with
/// its name, type and hash. Kept apart from the domain type, so renaming a property never breaks stored
/// entries. Pure logic.
/// </summary>
internal static class DocumentFileJson
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        Converters = { new JsonStringEnumConverter<DocumentFileKind>() },
    };

    public static string Write(DocumentFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return JsonSerializer.Serialize(new Stored(file.Kind, file.FileName, file.MediaType, file.SizeBytes, file.Sha256), s_options);
    }

    public static DocumentFile Read(string json)
    {
        var stored = JsonSerializer.Deserialize<Stored>(json, s_options) ?? throw new JsonException("A trashed file is no JSON null.");
        return new DocumentFile(stored.Kind, stored.FileName, stored.MediaType, stored.SizeBytes, stored.Sha256);
    }

    private sealed record Stored(
        [property: JsonPropertyName("kind")] DocumentFileKind Kind,
        [property: JsonPropertyName("fileName")] string FileName,
        [property: JsonPropertyName("mediaType")] string MediaType,
        [property: JsonPropertyName("sizeBytes")] long SizeBytes,
        [property: JsonPropertyName("sha256")] string Sha256);
}
