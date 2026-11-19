using System.Text.Json;

namespace LlmSpike.Runs;

/// <summary>One run on disk (<c>measurements/&lt;label&gt;.json</c>), so reports can be rebuilt without the model.</summary>
public sealed record RunFile(RunHeader Header, IReadOnlyList<RunRecord> Records)
{
    public void Save(string runsDirectory)
    {
        Directory.CreateDirectory(runsDirectory);
        var path = Path.Combine(runsDirectory, this.Header.Label + ".json");
        File.WriteAllText(path, JsonSerializer.Serialize(this, SpikeJson.Options));
    }

    public static IReadOnlyList<RunFile> LoadAll(string runsDirectory)
    {
        if (!Directory.Exists(runsDirectory))
        {
            return [];
        }

        return Directory.EnumerateFiles(runsDirectory, "*.json")
            .Order(StringComparer.Ordinal)
            .Select(path => JsonSerializer.Deserialize<RunFile>(File.ReadAllText(path), SpikeJson.Options)
                ?? throw new InvalidDataException($"{path} is empty."))
            .ToList();
    }
}
