using System.Text.Json.Serialization;

namespace NoMorePaper.Contracts.Editing;

/// <summary>How far a page is turned clockwise, relative to how it is stored now; serialized as its name.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PageRotationDto>))]
public enum PageRotationDto
{
    None = 0,
    Right = 90,
    Half = 180,
    Left = 270,
}
