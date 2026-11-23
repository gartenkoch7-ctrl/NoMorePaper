using System.Text.Json.Serialization;

namespace NoMorePaper.Contracts.Trash;

/// <summary>What lies in the trash; serialized as its name.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TrashItemKindDto>))]
public enum TrashItemKindDto
{
    /// <summary>A whole document.</summary>
    Document,

    /// <summary>The original of a document that keeps only its edited version.</summary>
    Original,

    /// <summary>An edited version.</summary>
    Edited,
}
