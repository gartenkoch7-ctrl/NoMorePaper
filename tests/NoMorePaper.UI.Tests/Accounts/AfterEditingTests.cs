using Microsoft.Playwright;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Accounts;

/// <summary>The account's default for edited documents: the card "Bearbeitete Dokumente".</summary>
public sealed class AfterEditingTests(ServerFixture server)
{
    private const string Card = "section[aria-labelledby=after-editing-heading]";

    private Task<BrowserSession> OpenAsync(
        FakeAccounts fake, string policy = "KeepBoth", int getStatus = 200, int putStatus = 204, string culture = SupportedCultures.German)
        => server.OpenAsync("/konto", culture, signedIn: true, async page =>
        {
            await fake.InstallAsync(page, signedIn: true);
            await fake.Mocks.RespondAsync(page, "GET", "/api/account/after-editing", getStatus, new { policy });
            await fake.Mocks.RespondAsync(page, "PUT", "/api/account/after-editing", putStatus, putStatus >= 400 ? new { status = putStatus } : null);
        });

    [Fact]
    public async Task Loads_TheCurrentChoice_WithAnExplanationPerRadio()
    {
        await using var session = await OpenAsync(new FakeAccounts(), "EditedOnly");
        var card = session.Page.Locator(Card);

        await Assertions.Expect(card.GetByRole(AriaRole.Group, new() { Name = "Nach dem Bearbeiten behalten" })).ToBeVisibleAsync();
        await Assertions.Expect(card.GetByRole(AriaRole.Radio)).ToHaveCountAsync(3);
        await Assertions.Expect(card.GetByRole(AriaRole.Radio, new() { Name = "Nur bearbeitete Fassung" })).ToBeCheckedAsync();
        await Assertions.Expect(card.GetByRole(AriaRole.Radio, new() { Name = "Beides" }))
            .ToHaveAccessibleDescriptionAsync("Original und bearbeitete Fassung bleiben.");
    }

    [Fact]
    public async Task Saving_SendsTheChoice_AndAnnouncesIt()
    {
        var fake = new FakeAccounts();
        await using var session = await OpenAsync(fake);
        var page = session.Page;

        await page.GetByRole(AriaRole.Radio, new() { Name = "Nur Original" }).CheckAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Speichern", Exact = true }).ClickAsync();

        await Assertions.Expect(page.Locator($"{Card} [role=status]")).ToHaveTextAsync("Einstellung gespeichert.");
        Assert.Contains("\"policy\":\"OriginalOnly\"", fake.Mocks.BodyOf("PUT", "/api/account/after-editing"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Saving_PutsFocusBackOnTheSaveButton()
    {
        await using var session = await OpenAsync(new FakeAccounts());
        var page = session.Page;

        await page.GetByRole(AriaRole.Radio, new() { Name = "Nur Original" }).CheckAsync();
        await page.Locator("#after-editing-save").ClickAsync();

        await Assertions.Expect(page.Locator($"{Card} [role=status]")).ToHaveTextAsync("Einstellung gespeichert.");
        await Assertions.Expect(page.Locator("#after-editing-save")).ToBeFocusedAsync();
    }

    [Fact]
    public async Task ASaveError_IsAnnounced_AndTheChoiceStays()
    {
        await using var session = await OpenAsync(new FakeAccounts(), putStatus: 500);
        var page = session.Page;

        await page.GetByRole(AriaRole.Radio, new() { Name = "Nur Original" }).CheckAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Speichern", Exact = true }).ClickAsync();

        await Assertions.Expect(page.Locator($"{Card} [role=alert]")).ToContainTextAsync("Die Einstellung konnte nicht gespeichert werden");
        await Assertions.Expect(page.GetByRole(AriaRole.Radio, new() { Name = "Nur Original" })).ToBeCheckedAsync();
    }

    [Fact]
    public async Task ALoadError_ShowsAnAlert_WithRetry()
    {
        await using var session = await OpenAsync(new FakeAccounts(), getStatus: 500);
        var card = session.Page.Locator(Card);

        await Assertions.Expect(card.Locator(".nmp-notice[role=alert]")).ToContainTextAsync("Die Einstellung konnte nicht geladen werden");
        await Assertions.Expect(card.GetByRole(AriaRole.Button, new() { Name = "Erneut versuchen" })).ToBeVisibleAsync();
    }

    [Theory]
    [InlineData(SupportedCultures.German)]
    [InlineData(SupportedCultures.English)]
    public async Task TheCard_HasNoAxeViolations(string culture)
    {
        await using var session = await OpenAsync(new FakeAccounts(), culture: culture);
        await Assertions.Expect(session.Page.Locator($"{Card} input[type=radio]")).ToHaveCountAsync(3);

        await AxeCheck.NoViolationsAsync(session.Page);
    }
}
