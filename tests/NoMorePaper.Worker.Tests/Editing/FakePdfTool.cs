using System.Globalization;
using NoMorePaper.Application.Editing;

namespace NoMorePaper.Worker.Tests.Editing;

/// <summary>
/// Stands in for qpdf: every source PDF has <see cref="PageCount"/> pages; composing writes a small "PDF" that
/// names the pages taken, so a test can tell versions apart and see what was composed.
/// </summary>
internal sealed class FakePdfTool : IPdfTool
{
    public int PageCount { get; set; } = 3;

    /// <summary>The pages of every compose call, in order; paths as file names.</summary>
    public List<IReadOnlyList<(string File, int Page, int Rotation)>> Composed { get; } = [];

    /// <summary>The content of the first source file of every compose call: which version the pages came from.</summary>
    public List<string> Sources { get; } = [];

    /// <summary>The compose call (counted from 1 over all calls) that fails like an interrupted qpdf; <c>null</c> for none.</summary>
    public int? FailOnCall { get; set; }

    public async Task ComposeAsync(IReadOnlyList<PdfPageSource> pages, string outputPath, CancellationToken cancellationToken)
    {
        if (Composed.Count + 1 == FailOnCall)
        {
            FailOnCall = null;
            throw new IOException("qpdf was interrupted");
        }

        var taken = pages.Select(page => (Path.GetFileName(page.Path), page.Page, (int)page.Rotation)).ToList();
        Composed.Add(taken);
        Sources.Add(await File.ReadAllTextAsync(pages[0].Path, cancellationToken));
        var text = "%PDF-1.7 " + string.Join(' ', taken.Select(page => string.Create(CultureInfo.InvariantCulture, $"{page.Item1}:{page.Page}:{page.Item3}")));
        await File.WriteAllTextAsync(outputPath, text, cancellationToken);
    }

    public Task<int> CountPagesAsync(string path, CancellationToken cancellationToken) => Task.FromResult(PageCount);
}
