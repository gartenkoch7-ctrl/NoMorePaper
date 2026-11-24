using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NoMorePaper.Contracts.Documents;
using NoMorePaper.UI.Tests.Documents;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Viewer;

/// <summary>
/// Thumbnails in the list and the search hits go through the shader and link to the viewer. Their address
/// carries the time of the edited version, so the cached thumbnail does not hide an edit.
/// </summary>
public sealed partial class ThumbnailTests(ServerFixture server)
{
    private static readonly DateTimeOffset s_editedAt = new(2026, 11, 24, 10, 30, 0, TimeSpan.Zero);

    private readonly ConcurrentBag<string> _requested = [];

    [Fact]
    public async Task DocumentList_ShowsThumbnailOfDoneDocument_AndLinksTitleToViewer()
    {
        var done = new DocumentSummaryDto(Guid.NewGuid(), "Steuerbescheid", DateTimeOffset.UtcNow, DocumentProcessingState.Done, EditedAt: s_editedAt);
        var queued = new DocumentSummaryDto(Guid.NewGuid(), "Stromrechnung", DateTimeOffset.UtcNow, DocumentProcessingState.Queued);
        await using var session = await ApiMocks.OpenViaNavigationAsync(server, SupportedCultures.German, "Dokumente", async page =>
        {
            await ApiMocks.ListAsync(page, new[] { done, queued });
            await ServeArchiveAsync(page);
        });
        var page = session.Page;
        // The table has symbol boxes; the first page is shown in the tiles.
        await page.GetByRole(AriaRole.Button, new() { Name = "Kacheln" }).ClickAsync();

        await Assertions.Expect(page.Locator("canvas[data-title=\"Steuerbescheid\"][aria-hidden=true]")).ToBeVisibleAsync();
        // Only a finished document has a PDF/A to show.
        await Assertions.Expect(page.Locator("canvas[data-title=\"Stromrechnung\"]:visible")).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Steuerbescheid" }))
            .ToHaveAttributeAsync("href", $"dokumente/{done.Id}");
        Assert.Contains($"/api/documents/{done.Id}/files/archive?v={s_editedAt.UtcTicks}", _requested);
    }

    [Fact]
    public async Task SearchHit_ShowsThumbnail_AndLinksToViewer()
    {
        var id = Guid.NewGuid();
        await using var session = await server.OpenAsync("/", SupportedCultures.English);
        var page = session.Page;
        await ApiMocks.SearchAsync(page, new[] { new SearchHitDto(id, "Invoice", DateTimeOffset.UtcNow, "…", 1, EditedAt: s_editedAt) });
        await ServeArchiveAsync(page);

        await page.Locator("#header-search-input").FillAsync("invoice");
        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(page.Locator("canvas[data-title=\"Invoice\"][aria-hidden=true]")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Invoice" })).ToHaveAttributeAsync("href", $"dokumente/{id}");
        Assert.Contains($"/api/documents/{id}/files/archive?v={s_editedAt.UtcTicks}", _requested);
    }

    private Task ServeArchiveAsync(IPage page) => page.RouteAsync(ArchiveUrl(), route =>
    {
        _requested.Add(new Uri(route.Request.Url).PathAndQuery);
        return route.FulfillAsync(
            new RouteFulfillOptions { Status = 200, ContentType = "application/pdf", BodyBytes = ViewerPdfs.Sample("01-brief-logo.pdf") });
    });

    [GeneratedRegex(@"/api/documents/[0-9a-f-]+/files/archive(\?v=[0-9]+)?$")]
    private static partial Regex ArchiveUrl();
}
