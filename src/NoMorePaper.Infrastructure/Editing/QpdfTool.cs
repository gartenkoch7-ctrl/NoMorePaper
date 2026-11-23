using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Editing;
using NoMorePaper.Application.Processing;
using NoMorePaper.Domain.Editing;
using NoMorePaper.Infrastructure.Ocr;

namespace NoMorePaper.Infrastructure.Editing;

/// <summary>
/// Page operations with qpdf (Apache-2.0, ADR-0025): <c>qpdf --empty --pages a.pdf 3,1 b.pdf 2 -- out.pdf</c>
/// takes pages without decoding them, so text layers and images stay as they are; <c>--rotate=+90:n</c>
/// turns page n of the result. Paths come from the working folder, never from received names.
/// </summary>
public sealed class QpdfTool(ExternalProcess process, TimeProvider time, ILogger<QpdfTool> logger) : IPdfTool
{
    private const string Qpdf = "qpdf";

    // qpdf: 0 success, 2 errors, 3 warnings only (the output is complete).
    private const int WarningsExitCode = 3;

    // "--show-npages" prints one number and a line break.
    private const int CountAnswerBytes = 64;

    /// <summary>Each call of qpdf must finish within this time.</summary>
    public static TimeSpan TimeLimit { get; } = TimeSpan.FromMinutes(2);

    public async Task ComposeAsync(IReadOnlyList<PdfPageSource> pages, string outputPath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        if (pages.Count == 0)
        {
            throw new ArgumentException("A PDF needs at least one page.", nameof(pages));
        }

        var started = time.GetTimestamp();
        await RunAsync(Arguments(pages, outputPath), cancellationToken).ConfigureAwait(false);
        var elapsedMs = (long)time.GetElapsedTime(started).TotalMilliseconds;
        logger.Composed(pages.Count, elapsedMs);
    }

    public async Task<int> CountPagesAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        // The answer is one number: read in memory of a fixed size, so no file lands next to the input
        // (which may lie in the archive) and an unexpected flood of output fails instead of growing.
        var buffer = new byte[CountAnswerBytes];
        using var answer = new MemoryStream(buffer, writable: true);
        var (exitCode, _) = await process.RunToStreamAsync(Qpdf, ["--show-npages", Path.GetFullPath(path)], answer, TimeLimit, cancellationToken)
            .ConfigureAwait(false);
        ThrowOnFailure(exitCode);
        var text = Encoding.ASCII.GetString(buffer, 0, (int)answer.Position);
        return int.Parse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The arguments for <see cref="ComposeAsync"/>: turns first, then the pages, consecutive pages of one
    /// file as one page list (qpdf keeps their order), then the output. Pure logic.
    /// </summary>
    public static IReadOnlyList<string> Arguments(IReadOnlyList<PdfPageSource> pages, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(pages);
        List<string> args = [];
        for (var i = 0; i < pages.Count; i++)
        {
            var page = pages[i];
            if (page.Page < 1 || !Enum.IsDefined(page.Rotation))
            {
                throw new ArgumentException("Pages start at 1 and turn by quarters.", nameof(pages));
            }

            if (page.Rotation != PageRotation.None)
            {
                args.Add(string.Create(CultureInfo.InvariantCulture, $"--rotate=+{(int)page.Rotation}:{i + 1}"));
            }
        }

        args.AddRange(["--empty", "--pages"]);
        foreach (var group in Groups(pages))
        {
            args.Add(Path.GetFullPath(group.Path));
            args.Add(string.Join(',', group.Pages.Select(page => page.ToString(CultureInfo.InvariantCulture))));
        }

        args.AddRange(["--", Path.GetFullPath(outputPath)]);
        return args;
    }

    private static List<(string Path, List<int> Pages)> Groups(IReadOnlyList<PdfPageSource> pages)
    {
        List<(string Path, List<int> Pages)> groups = [];
        foreach (var page in pages)
        {
            if (groups.Count > 0 && groups[^1].Path == page.Path)
            {
                groups[^1].Pages.Add(page.Page);
            }
            else
            {
                groups.Add((page.Path, [page.Page]));
            }
        }

        return groups;
    }

    private async Task RunAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var (exitCode, _) = await process.RunAsync(Qpdf, args, TimeLimit, cancellationToken).ConfigureAwait(false);
        ThrowOnFailure(exitCode);
    }

    // The tail of standard error is not passed on: it may quote document content.
    private static void ThrowOnFailure(int exitCode)
    {
        if (exitCode is not (0 or WarningsExitCode))
        {
            throw new ExternalToolException(Qpdf, exitCode, isPermanent: false);
        }
    }
}
