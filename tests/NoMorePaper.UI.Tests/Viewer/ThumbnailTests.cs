using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NoMorePaper.Contracts.Documents;
using NoMorePaper.UI.Tests.Documents;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Viewer;

/// <summary>Thumbnails in the list and the search hits go through the shader and link to the viewer.</summary>
public sealed partial class ThumbnailTests(ServerFixture server)
{
    [Fact]
    public async Task DocumentList_ShowsThumbnailOfDoneDocument_AndLinksTitleToViewer()
    {
        var done = new DocumentSummaryDto(Guid.NewGuid(), "Steuerbescheid", DateTimeOffset.UtcNow, DocumentProcessingState.Done);
        var queued = new DocumentSummaryDto(Guid.NewGuid(), "Stromrechnung", DateTimeOffset.UtcNow, DocumentProcessingState.Queued);
        await using var session = await ApiMocks.OpenViaNavigationAsync(server, SupportedCultures.German, "Dokumente", async page =>
        {
            await ApiMocks.ListAsync(page, new[] { done, queued });
            await ServeArchiveAsync(page);
        });
        var page = session.Page;

        await Assertions.Expect(page.Locator("canvas[data-title=\"Steuerbescheid\"][aria-hidden=true]")).ToBeVisibleAsync();
        // Only a finished document has a PDF/A to show.
        await Assertions.Expect(page.Locator("canvas[data-title=\"Stromrechnung\"]:visible")).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Steuerbescheid" }))
            .ToHaveAttributeAsync("href", $"dokumente/{done.Id}");
    }

    [Fact]
    public async Task SearchHit_ShowsThumbnail_AndLinksToViewer()
    {
        var id = Guid.NewGuid();
        await using var session = await server.OpenAsync("/", SupportedCultures.English);
        var page = session.Page;
        await ApiMocks.SearchAsync(page, new[] { new SearchHitDto(id, "Invoice", DateTimeOffset.UtcNow, "…", 1) });
        await ServeArchiveAsync(page);

        await page.Locator("#header-search-input").FillAsync("invoice");
        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(page.Locator("canvas[data-title=\"Invoice\"][aria-hidden=true]")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Invoice" })).ToHaveAttributeAsync("href", $"dokumente/{id}");
    }

    private static Task ServeArchiveAsync(IPage page) => page.RouteAsync(ArchiveUrl(), route => route.FulfillAsync(
        new RouteFulfillOptions { Status = 200, ContentType = "application/pdf", BodyBytes = ViewerPdfs.Sample("01-brief-logo.pdf") }));

    [GeneratedRegex(@"/api/documents/[0-9a-f-]+/files/archive$")]
    private static partial Regex ArchiveUrl();
}
