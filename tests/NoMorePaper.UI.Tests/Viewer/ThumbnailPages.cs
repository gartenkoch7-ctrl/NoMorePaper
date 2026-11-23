using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NoMorePaper.Contracts.Documents;
using NoMorePaper.UI.Tests.Documents;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;

namespace NoMorePaper.UI.Tests.Viewer;

/// <summary>The document list with one finished document, to look at its thumbnail.</summary>
internal static partial class ThumbnailPages
{
    public const string Title = "Blaue Box";

    /// <summary>
    /// Opens the list; the archive of its one document is <paramref name="pdf"/>.
    /// <paramref name="prepare"/> runs on the start page first.
    /// </summary>
    public static async Task<BrowserSession> OpenListAsync(ServerFixture server, byte[] pdf, Func<IPage, Task>? prepare = null)
    {
        var session = await ApiMocks.OpenViaNavigationAsync(server, SupportedCultures.German, "Dokumente", async page =>
        {
            if (prepare is not null)
            {
                await prepare(page);
            }

            var done = new DocumentSummaryDto(Guid.NewGuid(), Title, DateTimeOffset.UtcNow, DocumentProcessingState.Done);
            await ApiMocks.ListAsync(page, new[] { done });
            await page.RouteAsync(ArchiveUrl(), route => route.FulfillAsync(
                new RouteFulfillOptions { Status = 200, ContentType = "application/pdf", BodyBytes = pdf }));
        });
        try
        {
            // The table has symbol boxes; the first page is drawn in the tiles.
            await session.Page.GetByRole(AriaRole.Button, new() { Name = "Kacheln" }).ClickAsync();
            return session;
        }
        catch
        {
            await session.DisposeAsync();
            throw;
        }
    }

    public static ILocator Canvas(IPage page) => page.Locator($"canvas[data-title=\"{Title}\"]");

    /// <summary>Colours of the drawn thumbnail at page positions (fractions from the top left).</summary>
    public static async Task<Rgb[]> ReadAsync(IPage page, params (double X, double Y)[] points)
    {
        var colours = await Canvas(page).EvaluateAsync<int[][]>("""
            (canvas, points) => {
              const ctx = canvas.getContext('2d');
              return points.map(([x, y]) => Array.from(ctx.getImageData(
                Math.floor(x * canvas.width), Math.floor(y * canvas.height), 1, 1).data.slice(0, 3)));
            }
            """, points.Select(p => new[] { p.X, p.Y }).ToArray());
        return [.. colours.Select(c => new Rgb(c[0], c[1], c[2]))];
    }

    [GeneratedRegex(@"/api/documents/[0-9a-f-]+/files/archive$")]
    private static partial Regex ArchiveUrl();
}
