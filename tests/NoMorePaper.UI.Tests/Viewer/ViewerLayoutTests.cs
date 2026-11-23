using Microsoft.Playwright;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Viewer;

/// <summary>The frame of the document page: back bar, head, toolbar, full screen and the two columns.</summary>
public sealed class ViewerLayoutTests(ServerFixture server)
{
    private const string De = SupportedCultures.German;

    [Fact]
    public async Task BackBar_HasTheBackLink_ThePath_AndTheArrivalTime()
    {
        await using var viewer = await ViewerPages.OpenAsync(server, De, ViewerPdfs.TwoPages());
        var page = viewer.Page;

        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Zurück zur Liste" })).ToHaveAttributeAsync("href", "dokumente");
        var path = page.GetByRole(AriaRole.Navigation, new() { Name = "Pfad" });
        await Assertions.Expect(path.GetByRole(AriaRole.Link, new() { Name = "Dokumente" })).ToBeVisibleAsync();
        await Assertions.Expect(path.Locator("[aria-current=page]")).ToHaveTextAsync(ViewerPages.Title);
        await Assertions.Expect(page.Locator(".doc-view-received time[datetime]")).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task Head_HasOneH1_AndTheBadges()
    {
        await using var viewer = await ViewerPages.OpenAsync(server, De, ViewerPdfs.TwoPages(), isPrivate: true);
        var page = viewer.Page;

        await Assertions.Expect(page.Locator("main h1")).ToHaveCountAsync(1);
        await Assertions.Expect(page.Locator("main h1")).ToHaveTextAsync(ViewerPages.Title);
        await Assertions.Expect(page.Locator(".doc-view-badges")).ToContainTextAsync("Status: Fertig");
        await Assertions.Expect(page.Locator(".doc-view-badges")).ToContainTextAsync("Privat");
    }

    [Fact]
    public async Task Downloads_ShowShortTexts_AndKeepTheirLongNames()
    {
        await using var viewer = await ViewerPages.OpenAsync(server, De, ViewerPdfs.TwoPages());
        var links = viewer.Page.Locator(".doc-view-downloads a");

        await Assertions.Expect(links).ToHaveCountAsync(2);
        await Assertions.Expect(links.Nth(0)).ToHaveTextAsync("Original");
        await Assertions.Expect(links.Nth(1)).ToHaveTextAsync("Archiv-PDF");
        await Assertions.Expect(viewer.Page.GetByRole(AriaRole.Link, new() { Name = "Original herunterladen" })).ToBeVisibleAsync();
        await Assertions.Expect(viewer.Page.GetByRole(AriaRole.Link, new() { Name = "Archiv-PDF (PDF/A) herunterladen" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Downloads_WithoutAnArchive_OfferOnlyTheOriginal()
    {
        await using var viewer = await ViewerPages.OpenAsync(server, De, ViewerPdfs.TwoPages(), archive: false);

        await Assertions.Expect(viewer.Page.Locator(".doc-view-downloads a")).ToHaveCountAsync(1);
        await Assertions.Expect(viewer.Page.Locator(".doc-view-downloads a")).ToHaveTextAsync("Original");
    }

    [Theory]
    [InlineData("Vorherige Seite")]
    [InlineData("Nächste Seite")]
    [InlineData("Verkleinern")]
    [InlineData("Vergrößern")]
    [InlineData("100 %")]
    [InlineData("Breite anpassen")]
    [InlineData("Originalfarben")]
    public async Task ToolbarButtons_HaveAName(string name)
    {
        await using var viewer = await ViewerPages.OpenAsync(server, De, ViewerPdfs.TwoPages());

        await Assertions.Expect(viewer.Page.Locator(".viewer-bar").GetByRole(AriaRole.Button, new() { Name = name, Exact = true })).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task ToolbarSymbols_AreHiddenFromScreenReaders()
    {
        await using var viewer = await ViewerPages.OpenAsync(server, De, ViewerPdfs.TwoPages());
        await ViewerPages.WaitUntilShownAsync(viewer.Page);

        var visible = await viewer.Page.Locator(".viewer-bar svg").EvaluateAllAsync<int>("icons => icons.filter(icon => icon.getAttribute('aria-hidden') !== 'true').length");

        Assert.Equal(0, visible);
    }

    [Fact]
    public async Task Fullscreen_SwitchesTheViewerColumn_AndSaysSo()
    {
        await using var viewer = await ViewerPages.OpenAsync(server, De, ViewerPdfs.TwoPages());
        var page = viewer.Page;
        var button = page.GetByRole(AriaRole.Button, new() { Name = "Vollbild", Exact = true });
        // The opening announcement ("Seite 1 von 2 ...") would replace the one for full screen.
        await ViewerPages.WaitUntilShownAsync(page);

        await button.ClickAsync();

        await Assertions.Expect(button).ToHaveAttributeAsync("aria-pressed", "true");
        Assert.Equal("doc-view-viewer", await page.EvaluateAsync<string>("document.fullscreenElement?.id"));
        await Assertions.Expect(ViewerPages.Announcement(page)).ToContainTextAsync("Vollbild an");
        await page.EvaluateAsync("document.exitFullscreen()");
        await Assertions.Expect(button).ToHaveAttributeAsync("aria-pressed", "false");
    }

    [Fact]
    public async Task Fullscreen_WhereTheBrowserCannot_HasNoButton()
    {
        await using var viewer = await ViewerPages.OpenAsync(server, De, ViewerPdfs.TwoPages(),
            prepare: page => page.EvaluateAsync("Object.defineProperty(document, 'fullscreenEnabled', { value: false })"));
        await ViewerPages.WaitUntilShownAsync(viewer.Page);

        await Assertions.Expect(viewer.Page.GetByRole(AriaRole.Button, new() { Name = "Vollbild", Exact = true })).ToHaveCountAsync(0);
        await Assertions.Expect(viewer.Page.GetByRole(AriaRole.Button, new() { Name = "Originalfarben" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Order_SideColumnComesFirstInThePage_AndSitsRightOfTheViewerWhenWide()
    {
        await using var viewer = await ViewerPages.OpenAsync(server, De, ViewerPdfs.TwoPages(),
            prepare: page => page.SetViewportSizeAsync(1440, 900));
        var page = viewer.Page;

        var sideFirst = await page.EvaluateAsync<bool>(
            "!!(document.querySelector('.doc-view-side').compareDocumentPosition(document.querySelector('.doc-view-viewer')) & Node.DOCUMENT_POSITION_FOLLOWING)");
        var side = await page.Locator(".doc-view-side").BoundingBoxAsync();
        var view = await page.Locator(".doc-view-viewer").BoundingBoxAsync();

        Assert.True(sideFirst);
        Assert.True(side!.X > view!.X);
    }

    [Theory]
    [InlineData(390, 844)]
    [InlineData(320, 256)]
    public async Task SmallScreens_HaveNoHorizontalScrolling(int width, int height)
    {
        await using var viewer = await ViewerPages.OpenAsync(server, De, ViewerPdfs.TwoPages(),
            prepare: page => page.SetViewportSizeAsync(width, height));
        await ViewerPages.WaitUntilShownAsync(viewer.Page);

        var fits = await viewer.Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth");

        Assert.True(fits);
    }

    [Fact]
    public async Task ThePage_LoadsNothingFromOtherHosts()
    {
        var hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var viewer = await ViewerPages.OpenAsync(server, De, ViewerPdfs.TwoPages(), prepare: page =>
        {
            page.Request += (_, request) =>
            {
                if (Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) && uri.Scheme.StartsWith("http", StringComparison.Ordinal))
                {
                    lock (hosts)
                    {
                        hosts.Add(uri.Host);
                    }
                }
            };
            return Task.CompletedTask;
        });
        await ViewerPages.WaitUntilShownAsync(viewer.Page);

        lock (hosts)
        {
            Assert.All(hosts, host => Assert.Equal(server.BaseAddress.Host, host));
        }
    }
}
