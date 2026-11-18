using Xunit;

namespace NoMorePaper.Architecture.Tests;

/// <summary>AGENTS.md 2.2: code is organized by feature, never by type kind.</summary>
public sealed class ConventionTests
{
    private static readonly string[] s_forbiddenSegments = ["Services", "Helpers", "Utils", "Utilities"];

    [Fact]
    public void Namespaces_AreOrganizedByFeature()
    {
        var offenders = Layers.All
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.Namespace?.StartsWith(Layers.Prefix, StringComparison.Ordinal) == true)
            .Where(type => type.Namespace!.Split('.').Intersect(s_forbiddenSegments, StringComparer.Ordinal).Any())
            .Select(type => type.FullName)
            .ToList();

        Assert.True(offenders.Count == 0, "Organize by feature, not by type kind: " + string.Join(", ", offenders));
    }

    [Fact]
    public void Folders_AreOrganizedByFeature()
    {
        var offenders = SourceFiles.ProductionFolders()
            .Where(folder => folder.Split(Path.DirectorySeparatorChar).Intersect(s_forbiddenSegments, StringComparer.OrdinalIgnoreCase).Any())
            .ToList();

        Assert.True(offenders.Count == 0, "Organize by feature, not by type kind: " + string.Join(", ", offenders));
    }
}
