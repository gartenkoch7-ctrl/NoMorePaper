using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NoMorePaper.Contracts.Documents;
using NoMorePaper.UI.Tests.Accounts;
using NoMorePaper.UI.Tests.Classification;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Viewer;

/// <summary>The three tabs next to the viewer: Angaben, Erkannter Text, Verlauf.</summary>
public sealed class ViewerTabsTests(ServerFixture server)
{
    private const string De = SupportedCultures.German;

    private static ILocator Tab(IPage page, string name) =>
        page.GetByRole(AriaRole.Tab, new() { Name = name, Exact = true });

    [Fact]
    public async Task ThreeTabs_InANamedList_OnlyTheFirstPanelIsShown()
    {
        await using var viewer = await ViewerPages.OpenAsync(server, De, ViewerPdfs.TwoPages());
        var page = viewer.Page;
        var list = page.GetByRole(AriaRole.Tablist, new() { Name = "Bereiche des Dokuments" });

        await Assertions.Expect(list.GetByRole(AriaRole.Tab)).ToHaveCountAsync(3);
        await Assertions.Expect(Tab(page, "Angaben")).ToHaveAttributeAsync("aria-selected", "true");
        await Assertions.Expect(Tab(page, "Erkannter Text")).ToHaveAttributeAsync("aria-selected", "false");
        await Assertions.Expect(Tab(page, "Verlauf")).ToHaveAttributeAsync("aria-selected", "false");
        await Assertions.Expect(page.Locator("#doc-fields-panel")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#doc-text-panel")).ToBeHiddenAsync();
        await Assertions.Expect(page.Locator("#doc-history-panel")).ToBeHiddenAsync();
    }

    [Fact]
    public async Task ArrowKeys_HomeAndEnd_MoveAndSelectTheTab_AndFocusFollows()
    {
        await using var viewer = await ViewerPages.OpenAsync(server, De, ViewerPdfs.TwoPages());
        var page = viewer.Page;

        await Tab(page, "Angaben").FocusAsync();
        await page.Keyboard.PressAsync("ArrowRight");
        await Assertions.Expect(Tab(page, "Erkannter Text")).ToHaveAttributeAsync("aria-selected", "true");
        await Assertions.Expect(Tab(page, "Erkannter Text")).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("End");
        await Assertions.Expect(Tab(page, "Verlauf")).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Home");
        await Assertions.Expect(Tab(page, "Angaben")).ToBeFocusedAsync();
        await Assertions.Expect(page.Locator("#doc-fields-panel")).ToBeVisibleAsync();
    }

    [Theory]
    [InlineData(SupportedCultures.German, 0)]
    [InlineData(SupportedCultures.German, 1)]
    [InlineData(SupportedCultures.German, 2)]
    [InlineData(SupportedCultures.English, 0)]
    [InlineData(SupportedCultures.English, 1)]
    [InlineData(SupportedCultures.English, 2)]
    public async Task EveryTab_HasNoAxeViolations(string culture, int index)
    {
        await using var viewer = await ViewerPages.OpenAsync(server, culture, ViewerPdfs.TwoPages(), fields: ClassificationSamples.Automatic());
        var page = viewer.Page;
        await ViewerPages.WaitUntilShownAsync(page);

        await page.GetByRole(AriaRole.Tab).Nth(index).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Tab).Nth(index)).ToHaveAttributeAsync("aria-selected", "true");
        await Assertions.Expect(page.Locator("[role=tabpanel]:not([hidden]) :is(.doc-text, .doc-history, .fields-summary)").First).ToBeVisibleAsync();

        await AxeCheck.NoViolationsAsync(page);
    }

    [Fact]
    public async Task RecognizedText_LoadsOnFirstOpening_IsReachableByTab_AndNamed()
    {
        await using var viewer = await ViewerPages.OpenAsync(server, De, ViewerPdfs.TwoPages());
        var page = viewer.Page;
        await ViewerPages.WaitUntilShownAsync(page);
        Assert.True(await viewer.StaysAtAsync(0));

        await ViewerPages.SelectTabAsync(page, "Erkannter Text");
        var text = page.GetByRole(AriaRole.Region, new() { Name = "Erkannter Text" });
        await Assertions.Expect(text).ToContainTextAsync("Erste Zeile");
        await Assertions.Expect(text).ToContainTextAsync("Zweite Zeile");
        await Assertions.Expect(text).ToHaveAttributeAsync("tabindex", "0");
        await ViewerPages.SelectTabAsync(page, "Verlauf");
        await ViewerPages.SelectTabAsync(page, "Erkannter Text");

        Assert.True(await viewer.StaysAtAsync(1));
    }

    [Fact]
    public async Task Copy_PutsTheTextOnTheClipboard_AndSaysSo()
    {
        await using var viewer = await ViewerPages.OpenAsync(server, De, ViewerPdfs.TwoPages());
        var page = viewer.Page;
        await page.Context.GrantPermissionsAsync(["clipboard-read", "clipboard-write"]);

        await ViewerPages.SelectTabAsync(page, "Erkannter Text");
        await page.GetByRole(AriaRole.Button, new() { Name = "Erkannten Text kopieren" }).ClickAsync();

        await Assertions.Expect(page.Locator("#doc-text-panel .live-region")).ToHaveTextAsync("Der erkannte Text wurde kopiert.");
        var copied = await page.EvaluateAsync<string>("navigator.clipboard.readText()");
        Assert.Equal(ViewerPages.SampleText, copied.ReplaceLineEndings("\n"));
    }

    [Fact]
    public async Task RecognizedText_WhileQueued_SaysItComesLater_AndAsksNothing()
    {
        await using var viewer = await ViewerPages.OpenAsync(server, De, ViewerPdfs.TwoPages(),
            archive: false, state: DocumentProcessingState.Queued);

        await ViewerPages.SelectTabAsync(viewer.Page, "Erkannter Text");

        await Assertions.Expect(viewer.Page.Locator("#doc-text-panel")).ToContainTextAsync("Der Text steht bereit");
        Assert.True(await viewer.StaysAtAsync(0));
    }

    [Fact]
    public async Task RecognizedText_WhenEmpty_SaysNothingWasRecognized()
    {
        await using var viewer = await ViewerPages.OpenAsync(server, De, ViewerPdfs.TwoPages(), text: string.Empty);

        await ViewerPages.SelectTabAsync(viewer.Page, "Erkannter Text");

        await Assertions.Expect(viewer.Page.Locator("#doc-text-panel")).ToContainTextAsync("Auf diesem Dokument wurde kein Text erkannt.");
        await Assertions.Expect(viewer.Page.GetByRole(AriaRole.Button, new() { Name = "Erkannten Text kopieren" })).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task RecognizedText_OnAServerError_ShowsAnAlert_AndRetryAsksAgain()
    {
        await using var viewer = await ViewerPages.OpenAsync(server, De, ViewerPdfs.TwoPages(), textStatus: 500);
        var page = viewer.Page;

        await ViewerPages.SelectTabAsync(page, "Erkannter Text");
        await Assertions.Expect(page.Locator("#doc-text-panel [role=alert]")).ToContainTextAsync("konnte nicht geladen werden");
        await page.GetByRole(AriaRole.Button, new() { Name = "Erneut versuchen" }).ClickAsync();

        Assert.True(await WaitForRequestsAsync(viewer, 2));
    }

    [Fact]
    public async Task Retry_FailingAgain_FocusesTheRetryButton_AndSaysTheErrorOnlyOnce()
    {
        await using var viewer = await ViewerPages.OpenAsync(server, De, ViewerPdfs.TwoPages(), textStatus: 500);
        var page = viewer.Page;
        await ViewerPages.SelectTabAsync(page, "Erkannter Text");
        var retry = page.GetByRole(AriaRole.Button, new() { Name = "Erneut versuchen" });
        await retry.ClickAsync();

        await Assertions.Expect(retry).ToBeFocusedAsync();
        await Assertions.Expect(page.Locator("#doc-text-panel").GetByText("konnte nicht geladen werden")).ToHaveCountAsync(1);
        await Assertions.Expect(page.Locator("#doc-text-panel .live-region")).ToHaveTextAsync(string.Empty);
    }

    [Fact]
    public async Task Retry_ReturningNoText_MovesFocusToTheNote()
    {
        await using var viewer = await ViewerPages.OpenAsync(server, De, ViewerPdfs.TwoPages(), textStatus: 500);
        var page = viewer.Page;
        await ViewerPages.SelectTabAsync(page, "Erkannter Text");
        await page.RouteAsync(new Regex($"/api/documents/{viewer.Id}/text$"), route => route.FulfillAsync(
            new RouteFulfillOptions { Status = 200, ContentType = "application/json", Body = """{"text":""}""" }));

        await page.GetByRole(AriaRole.Button, new() { Name = "Erneut versuchen" }).ClickAsync();

        await Assertions.Expect(page.Locator("#doc-text-note")).ToBeFocusedAsync();
        await Assertions.Expect(page.Locator("#doc-text-note")).ToHaveTextAsync("Auf diesem Dokument wurde kein Text erkannt.");
    }

    [Fact]
    public async Task History_ListsTheSteps_WithATimeOnlyForTheArrival()
    {
        var fields = ClassificationSamples.Automatic() with { Source = "mail" };
        await using var viewer = await ViewerPages.OpenAsync(server, De, ViewerPdfs.TwoPages(), fields: fields);
        var page = viewer.Page;

        await ViewerPages.SelectTabAsync(page, "Verlauf");

        var items = page.Locator("#doc-history-panel ol.doc-history > li");
        await Assertions.Expect(items).ToHaveCountAsync(3);
        await Assertions.Expect(items.Nth(0)).ToContainTextAsync("Angaben automatisch erkannt");
        await Assertions.Expect(items.Nth(1)).ToContainTextAsync("Text erkannt");
        await Assertions.Expect(items.Nth(2)).ToContainTextAsync("Eingegangen");
        await Assertions.Expect(items.Nth(2)).ToContainTextAsync("über Mail");
        await Assertions.Expect(page.Locator("#doc-history-panel time")).ToHaveCountAsync(1);
        await Assertions.Expect(items.Nth(2).Locator("time")).ToHaveCountAsync(1);
        await Assertions.Expect(page.Locator("#doc-history-panel")).ToContainTextAsync("Zeitpunkte gibt es bisher nur für den Eingang.");
    }

    [Fact]
    public async Task OpenForm_SurvivesATabChange()
    {
        await using var viewer = await ViewerPages.OpenAsync(server, De, ViewerPdfs.TwoPages(), fields: ClassificationSamples.Automatic());
        var page = viewer.Page;

        await page.GetByRole(AriaRole.Button, new() { Name = "Bearbeiten", Exact = true }).ClickAsync();
        await page.Locator("#fields-title").FillAsync("Neuer Titel");
        await ViewerPages.SelectTabAsync(page, "Verlauf");
        await ViewerPages.SelectTabAsync(page, "Angaben");

        await Assertions.Expect(page.Locator("#fields-title")).ToHaveValueAsync("Neuer Titel");
    }

    // The retry runs asynchronously after the click; poll the counter instead of sleeping a fixed time.
    private static async Task<bool> WaitForRequestsAsync(ViewerSession viewer, int expected)
    {
        for (var i = 0; i < 50; i++)
        {
            if (viewer.TextRequests >= expected)
            {
                return true;
            }

            await Task.Delay(100);
        }

        return false;
    }
}
