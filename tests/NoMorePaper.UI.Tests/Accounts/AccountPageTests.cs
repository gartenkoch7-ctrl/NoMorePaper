using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Accounts;

/// <summary>The account page: who is signed in, change the password, sign out (here and everywhere).</summary>
public sealed class AccountPageTests(ServerFixture server)
{
    private const string Current = "Das-alte-Passwort";
    private const string New = "Das-neue-Passwort";

    private Task<BrowserSession> OpenAsync(FakeAccounts fake, int passwordStatus = 204)
        => server.OpenAsync("/konto", SupportedCultures.German, signedIn: true, async page =>
        {
            await fake.InstallAsync(page, signedIn: true);
            await fake.Mocks.RespondAsync(page, "POST", "/api/account/password", passwordStatus);
        });

    private static async Task FillAsync(IPage page, string current, string next, string repeat)
    {
        await page.Locator("#account-current").FillAsync(current);
        await page.Locator("#account-new").FillAsync(next);
        await page.Locator("#account-repeat").FillAsync(repeat);
        await page.Keyboard.PressAsync("Enter");
    }

    [Fact]
    public async Task ShowsWhoIsSignedIn_AndTheRole_AsText()
    {
        await using var session = await OpenAsync(new FakeAccounts());

        await Assertions.Expect(session.Page.Locator("main [data-ready]")).ToHaveTextAsync("Angemeldet als Anna Beispiel (anna). Verwalter");
    }

    [Fact]
    public async Task PasswordFields_HaveTheRightAutocomplete()
    {
        await using var session = await OpenAsync(new FakeAccounts());
        var page = session.Page;

        await Assertions.Expect(page.Locator("#account-current")).ToHaveAttributeAsync("autocomplete", "current-password");
        await Assertions.Expect(page.Locator("#account-new")).ToHaveAttributeAsync("autocomplete", "new-password");
        await Assertions.Expect(page.Locator("#account-repeat")).ToHaveAttributeAsync("autocomplete", "new-password");
    }

    [Fact]
    public async Task ChangePassword_Success_AnnouncesAndClearsTheFields()
    {
        var fake = new FakeAccounts();
        await using var session = await OpenAsync(fake);
        var page = session.Page;

        await FillAsync(page, Current, New, New);

        await Assertions.Expect(page.Locator("main [role=status]")).ToHaveTextAsync("Das Passwort wurde geändert.");
        await Assertions.Expect(page.Locator("#account-current")).ToHaveValueAsync(string.Empty);
        Assert.Contains("\"newPassword\":\"" + New + "\"", fake.Mocks.BodyOf("POST", "/api/account/password"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChangePassword_Rejected_IsAnnounced_AndFocusesTheCurrentPassword()
    {
        await using var session = await OpenAsync(new FakeAccounts(), passwordStatus: 400);
        var page = session.Page;

        await FillAsync(page, "falsch", New, New);

        await Assertions.Expect(page.Locator("main .form-error").First).ToContainTextAsync("Das Passwort wurde nicht geändert.");
        await Assertions.Expect(page.Locator("#account-current")).ToBeFocusedAsync();
    }

    [Fact]
    public async Task ChangePassword_TooShort_IsCaughtBeforeTheServer()
    {
        var fake = new FakeAccounts();
        await using var session = await OpenAsync(fake);
        var page = session.Page;

        await FillAsync(page, Current, "kurz", "kurz");

        await Assertions.Expect(page.Locator("main .form-error").First).ToHaveTextAsync("Fehler: Das Passwort braucht mindestens 10 Zeichen.");
        await Assertions.Expect(page.Locator("#account-new")).ToBeFocusedAsync();
        Assert.False(fake.Mocks.WasCalled("POST", "/api/account/password"));
    }

    [Theory]
    [InlineData("Abmelden", "/api/auth/logout")]
    [InlineData("Auf allen Geräten abmelden", "/api/auth/logout-everywhere")]
    public async Task SignOut_CallsTheRightRoute_AndGoesToSignIn(string button, string route)
    {
        var fake = new FakeAccounts();
        await using var session = await OpenAsync(fake);
        var page = session.Page;

        // "Abmelden" also names the header button; the one in the page is inside the main region.
        await page.Locator("main").GetByRole(AriaRole.Button, new() { Name = button, Exact = true }).ClickAsync();

        await Assertions.Expect(page).ToHaveURLAsync(new Regex("/anmelden$"));
        Assert.True(fake.Mocks.WasCalled("POST", route));
    }
}
