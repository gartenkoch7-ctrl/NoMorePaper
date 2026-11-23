using Microsoft.Playwright;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.UI.Tests.Keyboard;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Viewer;

/// <summary>The viewer works without a mouse: zoom 25–1600 %, page changes, announcements, text layer.</summary>
public sealed class ViewerKeyboardTests(ServerFixture server)
{
    [Fact]
    public async Task CtrlPlusMinusZero_ZoomFrom25To1600Percent_AndAnnounceIt()
    {
        await using var viewer = await ViewerPages.OpenAsync(server, SupportedCultures.German, ViewerPdfs.TwoPages());
        var page = viewer.Page;
        await ViewerPages.WaitUntilShownAsync(page);
        var zoom = page.Locator(".viewer-zoom");
        await Assertions.Expect(zoom).ToHaveTextAsync("Zoom 100 %");
        await page.Locator(ViewerPages.Scroller).FocusAsync();

        // 100 → 150 → 200 → 300 → 400 → 600 → 800 → 1200 → 1600, and one more stays at 1600.
        for (var i = 0; i < 9; i++)
        {
            await page.Keyboard.PressAsync("Control+NumpadAdd");
        }

        await Assertions.Expect(zoom).ToHaveTextAsync("Zoom 1600 %");
        await Assertions.Expect(ViewerPages.Announcement(page)).ToContainTextAsync("Zoom 1600 %");

        await page.Keyboard.PressAsync("Control+Digit0");
        await Assertions.Expect(zoom).ToHaveTextAsync("Zoom 100 %");

        for (var i = 0; i < 4; i++)
        {
            await page.Keyboard.PressAsync("Control+Minus");
        }

        await Assertions.Expect(zoom).ToHaveTextAsync("Zoom 25 %");
        await Assertions.Expect(ViewerPages.Announcement(page)).ToContainTextAsync("Zoom 25 %");
    }

    [Fact]
    public async Task ZoomKeys_OutsideTheDocumentPage_StayTheBrowsersZoom()
    {
        await using var viewer = await ViewerPages.OpenAsync(server, SupportedCultures.German, ViewerPdfs.TwoPages());
        var page = viewer.Page;
        await ViewerPages.WaitUntilShownAsync(page);
        // Runs after every other listener: records whether the browser's own zoom was blocked.
        await page.EvaluateAsync("""
            () => {
              window.prevented = [];
              window.addEventListener('keydown', (e) => { if (e.key !== 'Control') window.prevented.push(e.defaultPrevented); });
            }
            """);
        await Button(page, "Vergrößern").FocusAsync();

        string[] keys = ["Control+Minus", "Control+Digit0", "Control+NumpadAdd", "Control+NumpadAdd"];
        foreach (var key in keys)
        {
            await page.Keyboard.PressAsync(key);
        }

        var prevented = await page.EvaluateAsync<bool[]>("() => window.prevented");
        Assert.Equal(keys.Length, prevented.Length);
        Assert.DoesNotContain(true, prevented);

        // In the document page the same key zooms the document (one step from 100 %, not from 200 %).
        await page.Locator(ViewerPages.Scroller).FocusAsync();
        await page.Keyboard.PressAsync("Control+NumpadAdd");

        await Assertions.Expect(page.Locator(".viewer-zoom")).ToHaveTextAsync("Zoom 150 %");
        Assert.True(await page.EvaluateAsync<bool>("() => window.prevented.at(-1)"), "Ctrl + plus in the page was not taken over");
    }

    [Fact]
    public async Task PageDownAndUp_ChangePage_AndAnnouncePageAndMissingText()
    {
        await using var viewer = await ViewerPages.OpenAsync(server, SupportedCultures.German, ViewerPdfs.TwoPages());
        var page = viewer.Page;
        await ViewerPages.WaitUntilShownAsync(page);
        await Assertions.Expect(page.Locator(".viewer-page")).ToHaveTextAsync("Seite 1 von 2");
        await Assertions.Expect(ViewerPages.Announcement(page)).ToContainTextAsync("Seite 1 von 2");
        await page.Locator(ViewerPages.Scroller).FocusAsync();

        await page.Keyboard.PressAsync("PageDown");

        await Assertions.Expect(page.Locator(".viewer-page")).ToHaveTextAsync("Seite 2 von 2");
        await Assertions.Expect(ViewerPages.Announcement(page)).ToContainTextAsync("Seite 2 von 2");
        await Assertions.Expect(ViewerPages.Announcement(page)).ToContainTextAsync("keinen Text");
        await Assertions.Expect(page.Locator(".viewer-no-text")).ToBeVisibleAsync();

        await page.Keyboard.PressAsync("PageUp");

        await Assertions.Expect(page.Locator(".viewer-page")).ToHaveTextAsync("Seite 1 von 2");
        await Assertions.Expect(page.Locator(".viewer-no-text")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task TextLayer_GivesScreenReadersThePageText()
    {
        await using var viewer = await ViewerPages.OpenAsync(server, SupportedCultures.German, ViewerPdfs.TwoPages());
        var page = viewer.Page;
        await ViewerPages.WaitUntilShownAsync(page);

        await Assertions.Expect(page.Locator(".viewer-text")).ToContainTextAsync("Seite eins mit Text");
        // Inside the focusable, named region the screen reader lands on.
        await Assertions.Expect(page.GetByRole(AriaRole.Region, new() { Name = "Dokumentseite" }))
            .ToContainTextAsync("Seite eins mit Text");
    }

    [Fact]
    public async Task Scroller_IsReachableByTab_AndDescribedByKeyHelp()
    {
        await using var viewer = await ViewerPages.OpenAsync(server, SupportedCultures.German, ViewerPdfs.TwoPages());
        var page = viewer.Page;
        await ViewerPages.WaitUntilShownAsync(page);

        FocusState focus = new();
        for (var i = 0; i < 60 && !focus.ClassName.Contains("viewer-scroller", StringComparison.Ordinal); i++)
        {
            await page.Keyboard.PressAsync("Tab");
            focus = await FocusState.ReadAsync(page);
        }

        Assert.Contains("viewer-scroller", focus.ClassName, StringComparison.Ordinal);
        await Assertions.Expect(page.Locator(ViewerPages.Scroller)).ToHaveAttributeAsync("aria-describedby", "viewer-keys");
        await Assertions.Expect(page.Locator("#viewer-keys")).ToContainTextAsync("Strg + Plus");
    }

    [Fact]
    public async Task ToolbarButtons_ZoomAndTurnPages()
    {
        await using var viewer = await ViewerPages.OpenAsync(server, SupportedCultures.English, ViewerPdfs.TwoPages());
        var page = viewer.Page;
        await ViewerPages.WaitUntilShownAsync(page);

        await Button(page, "Zoom in").ClickAsync();
        await Assertions.Expect(page.Locator(".viewer-zoom")).ToHaveTextAsync("Zoom 150 %");
        await Button(page, "Zoom out").ClickAsync();
        await Button(page, "Zoom out").ClickAsync();
        await Assertions.Expect(page.Locator(".viewer-zoom")).ToHaveTextAsync("Zoom 75 %");
        await Button(page, "100 %").ClickAsync();
        await Assertions.Expect(page.Locator(".viewer-zoom")).ToHaveTextAsync("Zoom 100 %");
        await Button(page, "Fit width").ClickAsync();
        await Assertions.Expect(page.Locator(".viewer-zoom")).Not.ToHaveTextAsync("Zoom 100 %");

        await Button(page, "Next page").ClickAsync();
        await Assertions.Expect(page.Locator(".viewer-page")).ToHaveTextAsync("Page 2 of 2");
        await Button(page, "Previous page").ClickAsync();
        await Assertions.Expect(page.Locator(".viewer-page")).ToHaveTextAsync("Page 1 of 2");
    }

    [Fact]
    public async Task CtrlWheel_ZoomsOneStepPerNotch_AndSmoothlyForTouchpads()
    {
        await using var viewer = await ViewerPages.OpenAsync(server, SupportedCultures.German, ViewerPdfs.TwoPages());
        var page = viewer.Page;
        await ViewerPages.WaitUntilShownAsync(page);

        // Playwright's Ctrl + wheel never reaches the page in headless Chromium, so the event is
        // dispatched directly; the browser delivers the same event for a wheel or a touchpad pinch.
        await WheelAsync(page, -100);
        await Assertions.Expect(page.Locator(".viewer-zoom")).ToHaveTextAsync("Zoom 150 %");
        await WheelAsync(page, 20);
        // exp(-20 * 0.01) * 150 % = 123 %: a pinch zooms smoothly between the steps.
        await Assertions.Expect(page.Locator(".viewer-zoom")).ToHaveTextAsync("Zoom 123 %");
    }

    private static Task WheelAsync(IPage page, int deltaY) => page.Locator(ViewerPages.Scroller).EvaluateAsync("""
        (scroller, deltaY) => {
          const box = scroller.getBoundingClientRect();
          scroller.dispatchEvent(new WheelEvent('wheel', {
            deltaY, ctrlKey: true, clientX: box.x + 100, clientY: box.y + 100, bubbles: true, cancelable: true,
          }));
        }
        """, deltaY);

    private static ILocator Button(IPage page, string name) =>
        page.GetByRole(AriaRole.Button, new() { Name = name, Exact = true });
}
