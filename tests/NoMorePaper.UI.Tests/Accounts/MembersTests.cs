using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NoMorePaper.Contracts.Accounts;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Accounts;

/// <summary>The members page: administrator only, invitation and reset links, removal with confirmation.</summary>
public sealed class MembersTests(ServerFixture server)
{
    private static readonly AccountDto _admin = new(Guid.NewGuid(), "anna", "Anna Beispiel", true);
    private static readonly AccountDto _ben = new(Guid.NewGuid(), "ben", "Ben Beispiel", false);

    private const string ClipboardStub =
        "Object.defineProperty(navigator.clipboard, 'writeText', { value: async t => { window.__copied = t; } });";

    private async Task<BrowserSession> OpenAsync(AccountMocks mocks, bool admin = true)
    {
        var fake = new FakeAccounts { IsAdmin = admin };
        return await server.OpenAsync("/mitglieder", SupportedCultures.German, signedIn: true, async page =>
        {
            await fake.InstallAsync(page, signedIn: true);
            await page.AddInitScriptAsync(ClipboardStub);
            await mocks.RespondAsync(page, "GET", "/api/members", 200, new[] { _admin, _ben });
            await mocks.RespondAsync(page, "POST", "/api/invitations", 200,
                new LinkDto("/einladung/token123", DateTimeOffset.UtcNow.AddDays(7)));
            await mocks.RespondAsync(page, "POST", $"/api/members/{_ben.Id}/reset-link", 200,
                new LinkDto("/passwort/reset456", DateTimeOffset.UtcNow.AddHours(24)));
            await mocks.RespondAsync(page, "DELETE", $"/api/members/{_ben.Id}", 204);
        });
    }

    [Fact]
    public async Task NonAdministrator_SeesNoMembersLink_AndAnExplanationOnThePage()
    {
        await using var session = await OpenAsync(new AccountMocks(), admin: false);
        var page = session.Page;

        await Assertions.Expect(page.Locator("main [data-ready]")).ToHaveTextAsync("Diese Seite ist nur für den Verwalter.");
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Mitglieder", Exact = true })).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Einstellungen", Exact = true })).ToHaveAttributeAsync("aria-current", "page");
    }

    [Fact]
    public async Task Table_NamesEveryColumn_AndGivesTheButtonsTheMembersName()
    {
        await using var session = await OpenAsync(new AccountMocks());
        var page = session.Page;

        await Assertions.Expect(page.GetByRole(AriaRole.Columnheader)).ToHaveTextAsync(["Name", "Benutzername", "Rolle", "Aktionen"]);
        await Assertions.Expect(page.GetByRole(AriaRole.Row, new() { Name = "Anna Beispiel" })).ToContainTextAsync("Verwalter");
        // The administrator cannot be removed or reset here: no buttons in that row.
        await Assertions.Expect(page.GetByRole(AriaRole.Row, new() { Name = "Anna Beispiel" }).GetByRole(AriaRole.Button)).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Entfernen: Ben Beispiel" })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Rücksetzlink erstellen für Ben Beispiel" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Invite_ShowsTheLinkInAField_AnnouncesIt_AndCopiesIt()
    {
        var mocks = new AccountMocks();
        await using var session = await OpenAsync(mocks);
        var page = session.Page;

        await page.GetByRole(AriaRole.Button, new() { Name = "Neues Mitglied einladen" }).ClickAsync();

        var field = page.Locator("#invite-link");
        await Assertions.Expect(field).ToHaveValueAsync(new Regex("/einladung/token123$"));
        await Assertions.Expect(field).ToBeFocusedAsync();
        await Assertions.Expect(page.Locator("main [role=status]").First).ToContainTextAsync("Einladungslink erstellt");
        await Assertions.Expect(page.Locator(".link-result")).ToContainTextAsync("kann nur einmal benutzt werden");
        await page.GetByRole(AriaRole.Button, new() { Name = "Kopieren: Einladungslink" }).ClickAsync();
        await Assertions.Expect(page.Locator(".link-result .live-region")).ToHaveTextAsync("Einladungslink wurde kopiert.");
        Assert.Matches("/einladung/token123$", await page.EvaluateAsync<string>("window.__copied"));
    }

    [Fact]
    public async Task ResetLink_IsShownForThatMember()
    {
        var mocks = new AccountMocks();
        await using var session = await OpenAsync(mocks);
        var page = session.Page;

        await page.GetByRole(AriaRole.Button, new() { Name = "Rücksetzlink erstellen für Ben Beispiel" }).ClickAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Rücksetzlink für Ben Beispiel" })).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#reset-link")).ToHaveValueAsync(new Regex("/passwort/reset456$"));
        await Assertions.Expect(page.Locator("#reset-link")).ToBeFocusedAsync();
    }

    [Fact]
    public async Task Remove_AsksFirst_CancelIsTheDefault_EscapeCancels()
    {
        var mocks = new AccountMocks();
        await using var session = await OpenAsync(mocks);
        var page = session.Page;

        await page.GetByRole(AriaRole.Button, new() { Name = "Entfernen: Ben Beispiel" }).ClickAsync();

        var box = page.GetByRole(AriaRole.Group, new() { Name = "Ben Beispiel wirklich entfernen?" });
        await Assertions.Expect(box).ToBeVisibleAsync();
        await Assertions.Expect(box.GetByRole(AriaRole.Button, new() { Name = "Abbrechen" })).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(box).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Neues Mitglied einladen" })).ToBeFocusedAsync();
        Assert.False(mocks.WasCalled("DELETE", $"/api/members/{_ben.Id}"));
    }

    [Fact]
    public async Task Remove_Confirmed_DeletesAndAnnounces()
    {
        var mocks = new AccountMocks();
        await using var session = await OpenAsync(mocks);
        var page = session.Page;

        await page.GetByRole(AriaRole.Button, new() { Name = "Entfernen: Ben Beispiel" }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Ja, entfernen" }).ClickAsync();

        await Assertions.Expect(page.Locator("main [role=status]").First).ToContainTextAsync("Ben Beispiel wurde entfernt.");
        Assert.True(mocks.WasCalled("DELETE", $"/api/members/{_ben.Id}"));
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Neues Mitglied einladen" })).ToBeFocusedAsync();
    }
}
