using System.Collections.Concurrent;
using Microsoft.Playwright;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Documents;

/// <summary>The tile view of the document list and the switch between table and tiles.</summary>
public sealed class DocumentTilesTests(ServerFixture server)
{
    [Fact]
    public async Task Switch_ShowsTilesWithTheSameInformation_AndGoesBack()
    {
        await using var session = await ApiMocks.OpenViaNavigationAsync(server, SupportedCultures.German, "Dokumente",
            page => ApiMocks.ListAsync(page, SampleDocuments.OneOfEachState()));
        var page = session.Page;
        var group = page.GetByRole(AriaRole.Group, new() { Name = "Ansicht" });
        await Assertions.Expect(group.GetByRole(AriaRole.Button, new() { Name = "Tabelle" })).ToHaveAttributeAsync("aria-pressed", "true");

        await group.GetByRole(AriaRole.Button, new() { Name = "Kacheln" }).ClickAsync();

        await Assertions.Expect(group.GetByRole(AriaRole.Button, new() { Name = "Kacheln" })).ToHaveAttributeAsync("aria-pressed", "true");
        await Assertions.Expect(page.GetByRole(AriaRole.Table)).ToHaveCountAsync(0);
        await Assertions.Expect(page.Locator("main ul.doc-tiles > li")).ToHaveCountAsync(4);
        await Assertions.Expect(page.Locator("main ul.doc-tiles > li h2 a")).ToHaveCountAsync(4);
        var tile = page.Locator("main ul.doc-tiles > li").Filter(new() { HasText = "Stromrechnung" });
        await Assertions.Expect(tile).ToContainTextAsync("Wartet");
        await Assertions.Expect(page.Locator("main ul.doc-tiles > li").Filter(new() { HasText = "Steuerbescheid" })).ToContainTextAsync("Fertig");

        await group.GetByRole(AriaRole.Button, new() { Name = "Tabelle" }).ClickAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Table)).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Tile_OfAPrivateDocument_SaysPrivateAsText()
    {
        await using var session = await ApiMocks.OpenViaNavigationAsync(server, SupportedCultures.German, "Dokumente",
            page => ApiMocks.ListAsync(page, SampleDocuments.Rich()));
        var page = session.Page;

        await page.GetByRole(AriaRole.Button, new() { Name = "Kacheln" }).ClickAsync();

        var tile = page.Locator("main ul.doc-tiles > li").Filter(new() { HasText = "Stromrechnung" });
        await Assertions.Expect(tile.Locator(".nmp-badge").Filter(new() { HasText = "Privat" })).ToHaveCountAsync(1);
        await Assertions.Expect(tile.Locator(".doc-tile-meta")).ToContainTextAsync("Rechnung");
        await Assertions.Expect(tile.Locator(".doc-tile-amount")).ToHaveTextAsync("84,20 EUR");
    }

    [Fact]
    public async Task Paging_AlsoWorksInTiles()
    {
        var urls = new ConcurrentQueue<string>();
        await using var session = await ApiMocks.OpenViaNavigationAsync(server, SupportedCultures.German, "Dokumente", async page =>
        {
            await PagedMocks.ListAsync(page, 120, urls);
            await ApiMocks.CountsAsync(page, SampleDocuments.Counts(120));
        });
        var page = session.Page;
        await page.GetByRole(AriaRole.Button, new() { Name = "Kacheln" }).ClickAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Nächste Seite", Exact = true }).ClickAsync();

        await Assertions.Expect(page.Locator(".documents-count")).ToHaveTextAsync("Zeige 51–100 von 120 Dokumenten");
        Assert.Contains("page=2", urls.Last(), StringComparison.Ordinal);
    }
}
