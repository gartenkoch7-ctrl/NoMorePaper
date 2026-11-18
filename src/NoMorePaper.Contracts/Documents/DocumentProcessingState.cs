using System.Text.Json.Serialization;

namespace NoMorePaper.Contracts.Documents;

/// <summary>Processing state as the API reports it; serialized as its name, not as a number.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<DocumentProcessingState>))]
public enum DocumentProcessingState
{
    Queued,
    Processing,
    Done,
    Failed,
}
