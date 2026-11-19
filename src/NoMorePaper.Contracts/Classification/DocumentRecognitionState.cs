using System.Text.Json.Serialization;

namespace NoMorePaper.Contracts.Classification;

/// <summary>Where the fields of a document come from; serialized as its name, not as a number.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<DocumentRecognitionState>))]
public enum DocumentRecognitionState
{
    /// <summary>Not recognized (yet).</summary>
    None,

    /// <summary>Recognized automatically and not checked yet ("automatisch erkannt").</summary>
    Automatic,

    /// <summary>Confirmed or edited by a person.</summary>
    Confirmed,
}
