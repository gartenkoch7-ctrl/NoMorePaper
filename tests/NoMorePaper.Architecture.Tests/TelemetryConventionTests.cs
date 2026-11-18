using System.Globalization;
using System.Text.RegularExpressions;
using Xunit;

namespace NoMorePaper.Architecture.Tests;

/// <summary>
/// Telemetry rules of AGENTS.md 2.3 and docs/kb/telemetrie.md, checked on the source code: names
/// follow nomorepaper.&lt;feature&gt;.&lt;operation&gt;, log event ids are unique and in their
/// project's range, logs go through [LoggerMessage] only and carry no user content.
/// </summary>
public sealed partial class TelemetryConventionTests
{
    private static readonly Dictionary<string, (int Min, int Max)> s_eventRanges = new(StringComparer.Ordinal)
    {
        ["NoMorePaper.Server"] = (1000, 1999),
        ["NoMorePaper.Worker"] = (2000, 2999),
        ["NoMorePaper.Infrastructure"] = (3000, 3999),
        ["NoMorePaper.Web.Client"] = (4000, 4999),
    };

    [Fact]
    public void OperationAndInstrumentNames_FollowTheScheme()
    {
        var names = SourceFiles.Production()
            .SelectMany(file => NamedTelemetry().Matches(file.Text).Select(match => (file.Path, Name: match.Groups["name"].Value)))
            .ToList();
        var offenders = names
            .Where(found => !NameScheme().IsMatch(found.Name))
            .Select(found => $"{found.Name} ({Path.GetFileName(found.Path)})")
            .ToList();

        Assert.Contains(names, found => found.Name == "nomorepaper.server.startup");
        Assert.True(offenders.Count == 0, "Names must be nomorepaper.<feature>.<operation>: " + string.Join(", ", offenders));
    }

    [Fact]
    public void LogEventIds_AreUnique_AndInTheirProjectRange()
    {
        var ids = SourceFiles.Production()
            .SelectMany(file => EventIdDeclaration().Matches(file.Text)
                .Select(match => (file.Project, Id: int.Parse(match.Groups["id"].Value, CultureInfo.InvariantCulture))))
            .ToList();

        var duplicates = ids.GroupBy(found => found.Id).Where(group => group.Count() > 1).Select(group => group.Key).ToList();
        var outOfRange = ids
            .Where(found => !s_eventRanges.TryGetValue(found.Project, out var range) || found.Id < range.Min || found.Id > range.Max)
            .Select(found => $"{found.Id} ({found.Project})")
            .ToList();

        Assert.NotEmpty(ids);
        Assert.True(duplicates.Count == 0, "Duplicate event ids: " + string.Join(", ", duplicates));
        Assert.True(outOfRange.Count == 0, "Event id outside its project's range: " + string.Join(", ", outOfRange));
    }

    [Fact]
    public void Logs_UseLoggerMessageOnly()
    {
        var offenders = SourceFiles.Production()
            .Where(file => DirectLogCall().IsMatch(file.Text) || ConsoleOutput().IsMatch(file.Text))
            .Select(file => Path.GetFileName(file.Path))
            .ToList();

        Assert.True(offenders.Count == 0, "Log through [LoggerMessage] methods: " + string.Join(", ", offenders));
    }

    [Fact]
    public void LogPlaceholdersAndTags_CarryNoUserContent()
    {
        var keys = SourceFiles.Production()
            .SelectMany(file => TagKey().Matches(file.Text).Select(match => match.Groups["key"].Value)
                .Concat(LogMessage().Matches(file.Text)
                    .SelectMany(match => Placeholder().Matches(match.Groups["message"].Value).Select(p => p.Groups["name"].Value))))
            .ToList();

        Assert.Contains("ElapsedMs", keys);

        var offenders = keys.Where(key => UserContentHint().IsMatch(key)).Distinct(StringComparer.Ordinal).ToList();
        Assert.True(offenders.Count == 0, "Possible user content in telemetry: " + string.Join(", ", offenders));
    }

    [GeneratedRegex(@"(StartActivity|Create(Counter|Histogram|UpDownCounter|Gauge|ObservableCounter|ObservableGauge|ObservableUpDownCounter)(<[^>]+>)?)\(\s*""(?<name>[^""]+)""")]
    private static partial Regex NamedTelemetry();

    [GeneratedRegex(@"^nomorepaper(\.[a-z][a-z0-9_]*){2,}$")]
    private static partial Regex NameScheme();

    [GeneratedRegex(@"\[LoggerMessage\(\s*EventId\s*=\s*(?<id>\d+)")]
    private static partial Regex EventIdDeclaration();

    // Backs up CA1848, which already fails the build for these calls.
    [GeneratedRegex(@"\.Log(Trace|Debug|Information|Warning|Error|Critical)\s*\(")]
    private static partial Regex DirectLogCall();

    [GeneratedRegex(@"\b(Console\.(Write|WriteLine|Error)|Debug\.(Write|WriteLine|Print)|Trace\.(Write|WriteLine))\b")]
    private static partial Regex ConsoleOutput();

    [GeneratedRegex(@"(SetTag|AddTag)\(\s*""(?<key>[^""]+)""")]
    private static partial Regex TagKey();

    [GeneratedRegex(@"\[LoggerMessage\([^\]]*Message\s*=\s*""(?<message>[^""]*)""")]
    private static partial Regex LogMessage();

    [GeneratedRegex(@"\{(?<name>[A-Za-z][A-Za-z0-9_.]*)(:[^}]*)?\}")]
    private static partial Regex Placeholder();

    // Document content, names, paths, mail and credentials must never reach logs (AGENTS.md 2.3, 2.4).
    [GeneratedRegex("(?i)(path|file|folder|name|title|subject|content|text|body|mail|address|user|password|token|secret|query)")]
    private static partial Regex UserContentHint();
}
