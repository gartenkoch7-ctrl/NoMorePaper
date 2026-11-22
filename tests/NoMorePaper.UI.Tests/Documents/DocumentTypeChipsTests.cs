using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Playwright;
using NoMorePaper.Contracts.Documents;
using NoMorePaper.UI.Tests.Classification;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Documents;

/// <summary>The type chips of the document list: counts per type, pressed state, and the list follows the choice.</summary>
public sealed class DocumentTypeChipsTests(ServerFixture server)
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    // Counts as the server answers them: with a type chosen the total shrinks, the chips keep their numbers.
    private static async Task MockAsync(IPage page, ConcurrentQueue<string> urls)
    {
        await PagedMocks.ListAsync(page, 3, urls);
        await page.RouteAsync(new System.Text.RegularExpressions.Regex(@"/api/documents/counts"), route =>
        {
            var chosen = route.Request.Url.Contains("documentTypeId=", StringComparison.Ordinal);
            var counts = new DocumentCountsDto(
                chosen ? 2 : 3, 3, [new(ClassificationSamples.Rechnung, 2), new(ClassificationSamples.Vertrag, 1)]);
            return route.FulfillAsync(new RouteFulfillOptions
            {
                Status = 200,
                ContentType = "application/json",
                Body = JsonSerializer.Serialize(counts, _json),
            });
        });
    }

    private Task<BrowserSession> OpenAsync(ConcurrentQueue<string> urls) => ApiMocks.OpenViaNavigationAsync(
        server, SupportedCultures.German, "Dokumente", page => MockAsync(page, urls));

    [Fact]
    public async Task Chips_ShowEveryTypeWithItsCount_AndAllIsPressed()
    {
        await using var session = await OpenAsync(new ConcurrentQueue<string>());
        var page = session.Page;

        var group = page.GetByRole(AriaRole.Group, new() { Name = "Nach Dokumentart filtern" });

        await Assertions.Expect(group.GetByRole(AriaRole.Button, new() { Name = "Alle 3" })).ToHaveAttributeAsync("aria-pressed", "true");
        await Assertions.Expect(group.GetByRole(AriaRole.Button, new() { Name = "Rechnung 2" })).ToHaveAttributeAsync("aria-pressed", "false");
        await Assertions.Expect(group.GetByRole(AriaRole.Button, new() { Name = "Vertrag 1" })).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".doc-count")).ToHaveTextAsync("3 Dokumente");
    }

    [Fact]
    public async Task ChoosingAType_ReloadsFromPageOne_AnnouncesTheCount_AndKeepsTheFilterButtonName()
    {
        var urls = new ConcurrentQueue<string>();
        await using var session = await OpenAsync(urls);
        var page = session.Page;

        await page.GetByRole(AriaRole.Button, new() { Name = "Rechnung 2" }).ClickAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Rechnung 2" })).ToHaveAttributeAsync("aria-pressed", "true");
        await Assertions.Expect(page.Locator(".documents-count")).ToHaveTextAsync("2 Dokumente gefunden.");
        var last = urls.Last();
        Assert.Contains("documentTypeId=", last, StringComparison.Ordinal);
        Assert.Contains("page=1", last, StringComparison.Ordinal);
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Filter", Exact = true })).ToBeVisibleAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Alle 3" }).ClickAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Alle 3" })).ToHaveAttributeAsync("aria-pressed", "true");
        await Assertions.Expect(page.Locator(".documents-count")).ToHaveTextAsync("3 Dokumente gefunden.");
        Assert.DoesNotContain("documentTypeId=", urls.Last(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhenCountsFail_ThereAreNoChipsAndNoNumber_ButTheListWorks()
    {
        await using var session = await ApiMocks.OpenViaNavigationAsync(server, SupportedCultures.German, "Dokumente", async page =>
        {
            await ApiMocks.ListAsync(page, SampleDocuments.Many(2));
            await ApiMocks.CountsAsync(page, new { }, 500);
        });
        var page = session.Page;

        await Assertions.Expect(page.GetByRole(AriaRole.Table)).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".type-chips")).ToHaveCountAsync(0);
        await Assertions.Expect(page.Locator(".doc-count")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task WhenCountsFailWithATypeChosen_AllAndTheChosenTypeStayWithoutNumbers()
    {
        await using var session = await ApiMocks.OpenViaNavigationAsync(server, SupportedCultures.German, "Dokumente", async page =>
        {
            await MockAsync(page, new ConcurrentQueue<string>());
            await page.RouteAsync(new System.Text.RegularExpressions.Regex(@"/api/documents/counts\?.*documentTypeId="),
                route => route.FulfillAsync(new RouteFulfillOptions { Status = 500, ContentType = "application/problem+json", Body = "{}" }));
        });
        var page = session.Page;

        await page.GetByRole(AriaRole.Button, new() { Name = "Rechnung 2" }).ClickAsync();

        var chosen = page.GetByRole(AriaRole.Button, new() { Name = "Rechnung", Exact = true });
        await Assertions.Expect(chosen).ToHaveAttributeAsync("aria-pressed", "true");
        await Assertions.Expect(page.Locator(".type-chip-count")).ToHaveCountAsync(0);
        await page.GetByRole(AriaRole.Button, new() { Name = "Alle", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Alle 3" })).ToHaveAttributeAsync("aria-pressed", "true");
    }

    [Fact]
    public async Task AnOlderAnswer_ArrivingLate_DoesNotReplaceTheNewerList()
    {
        await using var session = await ApiMocks.OpenViaNavigationAsync(server, SupportedCultures.German, "Dokumente", async page =>
        {
            await MockAsync(page, new ConcurrentQueue<string>());
            await page.RouteAsync(new System.Text.RegularExpressions.Regex(@"/api/documents\?"), async route =>
            {
                var invoice = route.Request.Url.Contains(ClassificationSamples.Rechnung.Id.ToString(), StringComparison.Ordinal);
                if (invoice)
                {
                    await Task.Delay(1500);
                }

                var title = invoice ? "Rechnungsdokument" : route.Request.Url.Contains("documentTypeId=", StringComparison.Ordinal) ? "Vertragsdokument" : "Alle";
                await route.FulfillAsync(new RouteFulfillOptions
                {
                    Status = 200,
                    ContentType = "application/json",
                    Body = JsonSerializer.Serialize(new[] { new DocumentSummaryDto(Guid.NewGuid(), title, DateTimeOffset.UtcNow, DocumentProcessingState.Done) }, _json),
                });
            });
        });
        var page = session.Page;

        await page.GetByRole(AriaRole.Button, new() { Name = "Rechnung 2" }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Vertrag 1" }).ClickAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Vertragsdokument" })).ToBeVisibleAsync();
        await page.WaitForTimeoutAsync(2000);
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Rechnungsdokument" })).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Vertragsdokument" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task TheFilterArea_HasNoDocumentTypeField()
    {
        await using var session = await OpenAsync(new ConcurrentQueue<string>());
        var page = session.Page;

        await page.GetByRole(AriaRole.Button, new() { Name = "Filter", Exact = true }).ClickAsync();

        await Assertions.Expect(page.Locator("#filter-correspondent")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByLabel("Dokumentart", new() { Exact = true })).ToHaveCountAsync(0);
    }
}
