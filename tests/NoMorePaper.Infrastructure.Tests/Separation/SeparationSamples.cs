using System.Text.Json;
using NoMorePaper.Application.Editing;
using NoMorePaper.Domain.Editing;

namespace NoMorePaper.Infrastructure.Tests.Separation;

/// <summary>
/// The 13 self-made scanned stacks of <c>Separation/Samples</c> as page starts, with their known boundaries
/// (<c>expected.json</c>); the same set for the page signs alone and for the measurement with a real Ollama.
/// </summary>
internal static class SeparationSamples
{
    public static string Folder => Path.Combine(AppContext.BaseDirectory, "Separation", "Samples");

    public static IReadOnlyList<SeparationSample> Load()
    {
        using var expected = JsonDocument.Parse(File.ReadAllText(Path.Combine(Folder, "expected.json")));
        return [.. expected.RootElement.GetProperty("stacks").EnumerateObject().Select(stack => new SeparationSample(
            stack.Name,
            Pages(stack.Name),
            Numbers(stack.Value.GetProperty("boundaries")),
            Numbers(stack.Value.GetProperty("senderOnly"))))];
    }

    /// <summary>Where the suggestion starts a new document: the first page of every part but the first.</summary>
    public static IReadOnlyList<int> Boundaries(SplitSuggestion? suggestion) =>
        suggestion is null ? [] : [.. suggestion.Parts.Skip(1).Select(part => part.First)];

    private static List<PageInfo> Pages(string file)
    {
        using var stack = JsonDocument.Parse(File.ReadAllText(Path.Combine(Folder, file)));
        return [.. stack.RootElement.GetProperty("pages").EnumerateArray().Select((page, index) =>
        {
            var head = page.GetProperty("head").GetString() ?? string.Empty;
            return new PageInfo(
                index + 1,
                head.Length > PageInfo.MaxHeadLength ? head[..PageInfo.MaxHeadLength] : head,
                page.GetProperty("blank").GetBoolean(),
                HadTextLayer: false);
        })];
    }

    private static List<int> Numbers(JsonElement array) => [.. array.EnumerateArray().Select(value => value.GetInt32())];
}
