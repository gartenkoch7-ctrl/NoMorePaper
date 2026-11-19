using System.Globalization;

namespace LlmSpike;

/// <summary>Command line of the spike. See README.md for the calls used in the measurement.</summary>
public sealed record SpikeOptions(
    string Command,
    Uri Endpoint,
    string Model,
    string Label,
    int ContextTokens,
    int Threads,
    int HeadChars,
    int TailChars,
    int Examples,
    bool ColdPrefix,
    IReadOnlySet<string> Only,
    string SpikeDirectory)
{
    public string SamplesDirectory => Path.Combine(this.SpikeDirectory, "samples");

    public string RunsDirectory => Path.Combine(this.SpikeDirectory, "measurements");

    public static SpikeOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 1; i < args.Length; i++)
        {
            var name = args[i];
            var isFlag = i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal);
            values[name] = isFlag ? "true" : args[++i];
        }

        string Text(string name, string fallback) => values.GetValueOrDefault(name, fallback);
        int Number(string name, int fallback) => int.Parse(Text(name, fallback.ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);

        var model = Text("--model", string.Empty);
        return new SpikeOptions(
            args.Length > 0 ? args[0] : "help",
            // The container publishes on 127.0.0.1 only; 11434 may be taken by a host installation with GPU.
            new Uri(Text("--url", "http://127.0.0.1:11435/")),
            model,
            Text("--label", model.Replace(':', '_').Replace('/', '_')),
            Number("--num-ctx", 4096),
            Number("--threads", 4),
            Number("--head", 4500),
            Number("--tail", 1500),
            Number("--examples", 0),
            values.ContainsKey("--cold-prefix"),
            Text("--only", string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal),
            Text("--dir", FindSpikeDirectory()));
    }

    /// <summary>Walks up from the binary to the folder that holds <c>samples/expected.json</c>.</summary>
    private static string FindSpikeDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "samples", "expected.json")))
            {
                return directory.FullName;
            }
        }

        return Directory.GetCurrentDirectory();
    }
}
