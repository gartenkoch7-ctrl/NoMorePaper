using System.Collections.Concurrent;
using Microsoft.Playwright;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Documents;

/// <summary>The pager of the document list: range text, page buttons, rows per page and the focus after paging.</summary>
public sealed class DocumentPagerTests(ServerFixture server)
{
    private static async Task<BrowserSession> OpenAsync(ServerFixture server, ConcurrentQueue<string> urls, int? total)
    {
        return await ApiMocks.OpenViaNavigationAsync(server, SupportedCultures.German, "Dokumente", async page =>
        {
            await PagedMocks.ListAsync(page, 120, urls);
            if (total is { } count)
            {
                await ApiMocks.CountsAsync(page, SampleDocuments.Counts(count));
            }
        });
    }

    private static ILocator Button(IPage page, string name) => page.GetByRole(AriaRole.Button, new() { Name = name, Exact = true });

    [Fact]
    public async Task FirstPage_ShowsRangeAndNavigation_WithPreviousOff()
    {
        await using var session = await OpenAsync(server, new ConcurrentQueue<string>(), total: 120);
        var page = session.Page;

        await Assertions.Expect(page.Locator(".doc-range")).ToHaveTextAsync("Zeige 1–50 von 120 Dokumenten");
        var nav = page.GetByRole(AriaRole.Navigation, new() { Name = "Seiten der Dokumentliste" });
        await Assertions.Expect(nav.GetByRole(AriaRole.Button, new() { Name = "Seite 1", Exact = true })).ToHaveAttributeAsync("aria-current", "page");
        await Assertions.Expect(Button(page, "Vorherige Seite")).ToBeDisabledAsync();
    }

    [Fact]
    public async Task Next_LoadsThePage_AndAnnouncesTheRange()
    {
        var urls = new ConcurrentQueue<string>();
        await using var session = await OpenAsync(server, urls, total: 120);
        var page = session.Page;

        await Button(page, "Nächste Seite").ClickAsync();

        await Assertions.Expect(page.Locator(".documents-count")).ToHaveTextAsync("Zeige 51–100 von 120 Dokumenten");
        Assert.Contains("page=2", urls.Last(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnTheLastPage_NextIsOff_AndTheFocusStaysOnTheCurrentPage()
    {
        await using var session = await OpenAsync(server, new ConcurrentQueue<string>(), total: 120);
        var page = session.Page;

        await Button(page, "Nächste Seite").ClickAsync();
        await Assertions.Expect(page.Locator(".doc-range")).ToHaveTextAsync("Zeige 51–100 von 120 Dokumenten");
        await Button(page, "Nächste Seite").ClickAsync();

        await Assertions.Expect(Button(page, "Nächste Seite")).ToBeDisabledAsync();
        await Assertions.Expect(Button(page, "Seite 3")).ToBeFocusedAsync();
    }

    [Fact]
    public async Task RowsPerPage_StartsAgainAtPageOne()
    {
        var urls = new ConcurrentQueue<string>();
        await using var session = await OpenAsync(server, urls, total: 120);
        var page = session.Page;

        await page.GetByLabel("Zeilen pro Seite").SelectOptionAsync("10");

        await Assertions.Expect(page.Locator(".doc-range")).ToHaveTextAsync("Zeige 1–10 von 120 Dokumenten");
        var last = urls.Last();
        Assert.Contains("pageSize=10", last, StringComparison.Ordinal);
        Assert.Contains("page=1", last, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithoutATotal_ItShowsThePageNumber_AndNextFollowsTheRowsOnThePage()
    {
        await using var session = await OpenAsync(server, new ConcurrentQueue<string>(), total: null);
        var page = session.Page;

        await Assertions.Expect(page.Locator(".doc-range")).ToHaveTextAsync("Seite 1");
        await Assertions.Expect(Button(page, "Nächste Seite")).ToBeEnabledAsync();
        await Button(page, "Nächste Seite").ClickAsync();
        await Assertions.Expect(page.Locator(".doc-range")).ToHaveTextAsync("Seite 2");
        await Button(page, "Nächste Seite").ClickAsync();

        // The third page has the last 20 rows: fewer than a full page, so there is no next page.
        await Assertions.Expect(page.Locator(".doc-range")).ToHaveTextAsync("Seite 3");
        await Assertions.Expect(Button(page, "Nächste Seite")).ToBeDisabledAsync();
        await Assertions.Expect(Button(page, "Vorherige Seite")).ToBeFocusedAsync();
    }
}
