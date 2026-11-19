using Microsoft.Playwright;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Keyboard;

/// <summary>The app is fully usable with the keyboard, and focus is always clearly visible.</summary>
public sealed class KeyboardTests(ServerFixture server)
{
    private const double MinIndicatorWidthPx = 3;

    [Theory]
    [InlineData(SupportedCultures.German, "Zum Inhalt springen")]
    [InlineData(SupportedCultures.English, "Skip to content")]
    public async Task FirstTab_FocusesVisibleSkipLink(string culture, string skipLinkText)
    {
        await using var session = await server.OpenAsync("/", culture);
        var page = session.Page;

        await page.Keyboard.PressAsync("Tab");

        var focus = await FocusState.ReadAsync(page);
        Assert.Contains("skip-link", focus.ClassName, StringComparison.Ordinal);
        Assert.Equal(skipLinkText, focus.Text);
        Assert.True(focus.IndicatorWidth >= MinIndicatorWidthPx, $"Focus indicator {focus.IndicatorWidth}px");
        await Assertions.Expect(page.Locator(".skip-link")).ToBeInViewportAsync();
    }

    [Fact]
    public async Task SkipLink_MovesFocusToMainContent()
    {
        await using var session = await server.OpenAsync("/", SupportedCultures.German);
        var page = session.Page;

        await page.Keyboard.PressAsync("Tab");
        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(page.Locator("main#main-content")).ToBeFocusedAsync();
    }

    [Fact]
    public async Task TabOrder_ReachesLanguageSwitch_WithVisibleFocusOnEveryStop()
    {
        await using var session = await server.OpenAsync("/", SupportedCultures.German);
        var page = session.Page;
        string[] expected =
        [
            "Zum Inhalt springen", "Startseite", "Hochladen", "Dokumente", "Zu prüfen", "Suche", "Listen", "Konto", "Mitglieder", "", "Suchen",
            "Abmelden", "Deutsch", "English",
        ];

        var stops = new List<FocusState>();
        foreach (var _ in expected)
        {
            await page.Keyboard.PressAsync("Tab");
            stops.Add(await FocusState.ReadAsync(page));
        }

        Assert.Equal(expected, stops.Select(stop => stop.Text));
        Assert.All(stops, stop => Assert.True(
            stop.IndicatorWidth >= MinIndicatorWidthPx,
            $"'{stop.Text}' has a {stop.IndicatorWidth}px focus indicator"));
    }

    [Fact]
    public async Task LanguageSwitch_WorksWithKeyboardOnly()
    {
        // No preselected culture: the preselection script would run again after the reload.
        await using var session = await server.OpenAsync("/");
        var page = session.Page;

        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "English" }).FocusAsync();
        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(page.Locator("html")).ToHaveAttributeAsync("lang", SupportedCultures.English);
        await Assertions.Expect(page.Locator(".skip-link")).ToHaveTextAsync("Skip to content");
    }
}
