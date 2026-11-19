using System.Text.Json;

namespace LlmSpike.Expected;

/// <summary>All samples with their expected values, loaded from <c>samples/expected.json</c>.</summary>
public sealed record SampleSet(Household Household, IReadOnlyList<ExpectedDocument> Documents)
{
    public static SampleSet Load(string samplesDirectory)
    {
        var json = File.ReadAllText(Path.Combine(samplesDirectory, "expected.json"));
        return JsonSerializer.Deserialize<SampleSet>(json, SpikeJson.Options)
            ?? throw new InvalidDataException("expected.json is empty.");
    }

    public static string ReadText(string samplesDirectory, ExpectedDocument document) =>
        File.ReadAllText(Path.Combine(samplesDirectory, document.Id + ".txt"));
}
