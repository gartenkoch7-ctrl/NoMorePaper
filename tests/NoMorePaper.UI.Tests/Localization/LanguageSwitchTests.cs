using Microsoft.Playwright;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Localization;

/// <summary>German is the default; the language switch changes and remembers the language.</summary>
public sealed class LanguageSwitchTests(ServerFixture server)
{
    [Fact]
    public async Task NothingStored_StartsInGerman()
    {
        await using var session = await server.OpenAsync("/");
        var page = session.Page;

        Assert.Equal(SupportedCultures.German, await page.Locator("html").GetAttributeAsync("lang"));
        await Assertions.Expect(page.Locator(".skip-link")).ToHaveTextAsync("Zum Inhalt springen");
        await Assertions.Expect(LanguageButton(page, "Deutsch")).ToHaveAttributeAsync("aria-pressed", "true");
        await Assertions.Expect(LanguageButton(page, "English")).ToHaveAttributeAsync("aria-pressed", "false");
    }

    [Fact]
    public async Task SwitchToEnglish_TranslatesUi_AndSurvivesReload()
    {
        await using var session = await server.OpenAsync("/");
        var page = session.Page;

        await LanguageButton(page, "English").ClickAsync();

        await Assertions.Expect(page.Locator("html")).ToHaveAttributeAsync("lang", SupportedCultures.English);
        await Assertions.Expect(LanguageButton(page, "English")).ToHaveAttributeAsync("aria-pressed", "true");
        await Assertions.Expect(page.GetByRole(AriaRole.Navigation, new PageGetByRoleOptions { Name = "Main navigation" }))
            .ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Group, new PageGetByRoleOptions { Name = "Language" }))
            .ToBeVisibleAsync();

        await page.ReloadAsync();

        await Assertions.Expect(page.Locator("h1")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("html")).ToHaveAttributeAsync("lang", SupportedCultures.English);
        await Assertions.Expect(page.Locator(".skip-link")).ToHaveTextAsync("Skip to content");
    }

    [Fact]
    public async Task LanguageButtons_AreNamedInTheirOwnLanguage()
    {
        await using var session = await server.OpenAsync("/", SupportedCultures.English);
        var page = session.Page;

        await Assertions.Expect(LanguageButton(page, "Deutsch")).ToHaveAttributeAsync("lang", SupportedCultures.German);
        await Assertions.Expect(LanguageButton(page, "English")).ToHaveAttributeAsync("lang", SupportedCultures.English);
    }

    private static ILocator LanguageButton(IPage page, string name) =>
        page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = name, Exact = true });
}
