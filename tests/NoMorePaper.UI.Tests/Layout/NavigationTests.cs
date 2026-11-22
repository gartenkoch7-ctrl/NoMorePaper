using Microsoft.Playwright;
using NoMorePaper.UI.Tests.Accounts;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Layout;

/// <summary>Side navigation with five main items (M4u step 0) and the menu on narrow screens.</summary>
public sealed class NavigationTests(ServerFixture server)
{
    [Theory]
    [InlineData("/", "Startseite")]
    [InlineData("/hochladen", "Hinzufügen")]
    [InlineData("/kamera", "Hinzufügen")]
    [InlineData("/dokumente", "Dokumente")]
    [InlineData("/suche", "Dokumente")]
    [InlineData("/konto", "Einstellungen")]
    [InlineData("/listen", "Einstellungen")]
    public async Task SubPages_MarkTheirMainItemAsCurrent(string path, string mainItem)
    {
        await using var session = await server.OpenAsync(path, SupportedCultures.German);
        var nav = session.Page.GetByRole(AriaRole.Navigation, new() { Name = "Hauptnavigation" });

        await Assertions.Expect(nav.GetByRole(AriaRole.Link, new() { Name = mainItem, Exact = true }))
            .ToHaveAttributeAsync("aria-current", "page");
        await Assertions.Expect(nav.Locator("[aria-current=page]")).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task AddPage_LinksToUploadAndCamera()
    {
        await using var session = await server.OpenAsync("/hinzufuegen", SupportedCultures.German);
        var main = session.Page.Locator("main");

        await Assertions.Expect(main.GetByRole(AriaRole.Link, new() { Name = "Hochladen" })).ToHaveAttributeAsync("href", "hochladen");
        await Assertions.Expect(main.GetByRole(AriaRole.Link, new() { Name = "Kamera" })).ToHaveAttributeAsync("href", "kamera");
    }

    [Theory]
    [InlineData(true, 4)]
    [InlineData(false, 3)]
    public async Task SettingsPage_ShowsMembersOnlyToTheAdministrator(bool admin, int links)
    {
        var fake = new FakeAccounts { IsAdmin = admin };
        await using var session = await server.OpenAsync("/einstellungen", SupportedCultures.German, signedIn: true,
            page => fake.InstallAsync(page, signedIn: true));
        var tiles = session.Page.Locator("main .link-tile");

        await Assertions.Expect(tiles).ToHaveCountAsync(links);
        await Assertions.Expect(session.Page.Locator("main").GetByRole(AriaRole.Link, new() { Name = "Mitglieder" }))
            .ToHaveCountAsync(admin ? 1 : 0);
    }

    [Fact]
    public async Task NarrowScreen_MenuButtonOpensAndClosesTheNavigation()
    {
        await using var session = await server.OpenAsync("/", SupportedCultures.German,
            prepare: page => page.SetViewportSizeAsync(390, 844));
        var page = session.Page;
        var menu = page.GetByRole(AriaRole.Button, new() { Name = "Menü" });
        var nav = page.GetByRole(AriaRole.Navigation, new() { Name = "Hauptnavigation" });

        await Assertions.Expect(menu).ToHaveAttributeAsync("aria-expanded", "false");
        await Assertions.Expect(nav).ToBeHiddenAsync();

        await menu.ClickAsync();
        await Assertions.Expect(menu).ToHaveAttributeAsync("aria-expanded", "true");
        await Assertions.Expect(nav).ToBeVisibleAsync();

        await nav.GetByRole(AriaRole.Link, new() { Name = "Dokumente", Exact = true }).ClickAsync();
        await Assertions.Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/dokumente$"));
        await Assertions.Expect(nav).ToBeHiddenAsync();
    }

    [Fact]
    public async Task NarrowScreen_OpeningTheMenu_FocusesTheFirstLink_AndEscapeReturnsToTheButton()
    {
        await using var session = await server.OpenAsync("/", SupportedCultures.German,
            prepare: page => page.SetViewportSizeAsync(390, 844));
        var page = session.Page;
        var menu = page.GetByRole(AriaRole.Button, new() { Name = "Menü" });
        var nav = page.GetByRole(AriaRole.Navigation, new() { Name = "Hauptnavigation" });

        await menu.FocusAsync();
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(nav.GetByRole(AriaRole.Link, new() { Name = "Startseite" })).ToBeFocusedAsync();

        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(nav).ToBeHiddenAsync();
        await Assertions.Expect(menu).ToBeFocusedAsync();
    }

    [Fact]
    public async Task NarrowScreen_NavigatingFromTheHeader_ClosesTheMenu()
    {
        await using var session = await server.OpenAsync("/", SupportedCultures.German,
            prepare: page => page.SetViewportSizeAsync(390, 844));
        var page = session.Page;
        var nav = page.GetByRole(AriaRole.Navigation, new() { Name = "Hauptnavigation" });

        await page.GetByRole(AriaRole.Button, new() { Name = "Menü" }).ClickAsync();
        await Assertions.Expect(nav).ToBeVisibleAsync();
        await page.Locator("header").GetByRole(AriaRole.Link, new() { Name = "Hochladen" }).ClickAsync();

        await Assertions.Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/hochladen$"));
        await Assertions.Expect(nav).ToBeHiddenAsync();
    }

    [Theory]
    [InlineData(SupportedCultures.German)]
    [InlineData(SupportedCultures.English)]
    public async Task NarrowScreen_WithTheMenuOpen_HasNoAxeViolations(string culture)
    {
        await using var session = await server.OpenAsync("/", culture,
            prepare: page => page.SetViewportSizeAsync(390, 844));
        var page = session.Page;
        await page.Locator(".menu-toggle").ClickAsync();
        await Assertions.Expect(page.Locator(".menu-toggle")).ToHaveAttributeAsync("aria-expanded", "true");

        await AxeCheck.NoViolationsAsync(page);
    }

    [Fact]
    public async Task WideScreen_HasNoMenuButton_AndAlwaysShowsTheNavigation()
    {
        await using var session = await server.OpenAsync("/", SupportedCultures.German);
        var page = session.Page;

        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Menü" })).ToBeHiddenAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Navigation, new() { Name = "Hauptnavigation" })).ToBeVisibleAsync();
    }
}
