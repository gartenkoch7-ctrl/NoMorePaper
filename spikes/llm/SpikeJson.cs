using System.Text.Encodings.Web;
using System.Text.Json;

namespace LlmSpike;

/// <summary>One JSON configuration for sample, run and request files.</summary>
public static class SpikeJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        // Keeps umlauts readable in the committed run files.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
