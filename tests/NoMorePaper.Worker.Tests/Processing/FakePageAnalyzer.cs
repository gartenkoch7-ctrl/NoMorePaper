using NoMorePaper.Application.Editing;

namespace NoMorePaper.Worker.Tests.Processing;

/// <summary>Stands in for the page analysis: answers <see cref="Pages"/> and remembers what it was asked.</summary>
internal sealed class FakePageAnalyzer : IPageAnalyzer
{
    /// <summary>What every call returns; one scanned page, so recognition follows OCR directly (a stack would go to separation).</summary>
    public IReadOnlyList<PageInfo> Pages { get; set; } = [new PageInfo(1, "Rechnung Stromanbieter", false, false)];

    /// <summary>Whether the archive PDF existed when it was analyzed.</summary>
    public bool? ArchiveExisted { get; private set; }

    public Task<IReadOnlyList<PageInfo>> AnalyzeAsync(string pdfPath, string originalPath, CancellationToken cancellationToken)
    {
        ArchiveExisted = File.Exists(pdfPath);
        return Task.FromResult(Pages);
    }
}
