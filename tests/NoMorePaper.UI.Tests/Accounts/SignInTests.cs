using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NoMorePaper.UI.Tests.Documents;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.UI.Tests.Keyboard;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Accounts;

/// <summary>Sign-in and sign-out: labels, keyboard, announcements, focus, redirects (browser mocks).</summary>
public sealed class SignInTests(ServerFixture server)
{
    private async Task<BrowserSession> OpenSignedOutAsync(
        string path, FakeAccounts fake, string culture = SupportedCultures.German)
        => await server.OpenAsync(path, culture, signedIn: false, async page =>
        {
            await fake.InstallAsync(page);
            await ApiMocks.ListAsync(page, Array.Empty<object>());
        });

    [Fact]
    public async Task Fields_HaveVisibleLabels_AndAutocompleteForPasswordManagers()
    {
        await using var session = await OpenSignedOutAsync("/anmelden", new FakeAccounts());
        var page = session.Page;

        await Assertions.Expect(page.GetByLabel("Benutzername")).ToHaveAttributeAsync("autocomplete", "username");
        await Assertions.Expect(page.GetByLabel("Passwort", new() { Exact = true })).ToHaveAttributeAsync("autocomplete", "current-password");
        await Assertions.Expect(page.Locator("label[for='signin-user']")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("label[for='signin-password']")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ShowPasswordToggle_ChangesTypeAndAriaPressed_ButKeepsItsName()
    {
        await using var session = await OpenSignedOutAsync("/anmelden", new FakeAccounts());
        var page = session.Page;
        var toggle = page.GetByRole(AriaRole.Button, new() { Name = "Passwort anzeigen: Passwort" });

        await Assertions.Expect(toggle).ToHaveAttributeAsync("aria-pressed", "false");
        await Assertions.Expect(page.Locator("#signin-password")).ToHaveAttributeAsync("type", "password");
        await toggle.FocusAsync();
        await page.Keyboard.PressAsync("Space");

        await Assertions.Expect(toggle).ToHaveAttributeAsync("aria-pressed", "true");
        await Assertions.Expect(page.Locator("#signin-password")).ToHaveAttributeAsync("type", "text");
    }

    [Fact]
    public async Task SignIn_WithKeyboardOnly_ShowsTheApp_AndSignOutReturnsToSignIn()
    {
        var fake = new FakeAccounts();
        await using var session = await OpenSignedOutAsync("/anmelden", fake);
        var page = session.Page;

        await page.Locator("#signin-user").FocusAsync();
        await page.Keyboard.TypeAsync("anna");
        await page.Keyboard.PressAsync("Tab");
        await page.Keyboard.TypeAsync("geheimes-passwort");
        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(page.Locator("h1")).ToHaveTextAsync("NoMorePaper");
        await Assertions.Expect(page.Locator(".account-name")).ToContainTextAsync("Anna Beispiel");
        Assert.Contains("\"userName\":\"anna\"", fake.Mocks.BodyOf("POST", "/api/auth/login"), StringComparison.Ordinal);

        await page.GetByRole(AriaRole.Button, new() { Name = "Abmelden" }).ClickAsync();

        await Assertions.Expect(page).ToHaveURLAsync(new Regex("/anmelden$"));
        await Assertions.Expect(page.GetByRole(AriaRole.Navigation)).ToHaveCountAsync(0);
        Assert.True(fake.Mocks.WasCalled("POST", "/api/auth/logout"));
    }

    [Fact]
    public async Task OpeningAProtectedPage_WithoutSession_GoesToSignIn_AndBackAfterwards()
    {
        var fake = new FakeAccounts();
        await using var session = await OpenSignedOutAsync("/dokumente", fake);
        var page = session.Page;

        await Assertions.Expect(page).ToHaveURLAsync(new Regex(@"/anmelden\?ziel=%2Fdokumente$"));
        await page.Locator("#signin-user").FillAsync("anna");
        await page.Locator("#signin-password").FillAsync("geheimes-passwort");
        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(page).ToHaveURLAsync(new Regex("/dokumente$"));
        await Assertions.Expect(page.Locator("h1")).ToHaveTextAsync("Dokumente");
    }

    [Theory]
    [InlineData("https://boese.example/")]
    [InlineData("//boese.example/")]
    public async Task SignIn_NeverLeavesTheApp_ThroughTheTargetParameter(string target)
    {
        var fake = new FakeAccounts();
        await using var session = await OpenSignedOutAsync($"/anmelden?ziel={Uri.EscapeDataString(target)}", fake);
        var page = session.Page;

        await page.Locator("#signin-user").FillAsync("anna");
        await page.Locator("#signin-password").FillAsync("geheimes-passwort");
        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(page.Locator(".account-name")).ToBeVisibleAsync();
        Assert.Equal(new Uri(server.BaseAddress, "/"), new Uri(page.Url.Split('?')[0]));
    }

    [Fact]
    public async Task WrongPassword_IsAnnouncedAsAlert_ClearsThePassword_AndFocusesIt()
    {
        await using var session = await OpenSignedOutAsync("/anmelden", new FakeAccounts { LoginStatus = 401 });
        var page = session.Page;

        await page.Locator("#signin-user").FillAsync("anna");
        await page.Locator("#signin-password").FillAsync("falsch");
        await page.Keyboard.PressAsync("Enter");

        var alert = page.Locator("main [role=alert]");
        await Assertions.Expect(alert).ToHaveTextAsync("Fehler: Benutzername oder Passwort stimmen nicht.");
        await Assertions.Expect(page.Locator("#signin-password")).ToBeFocusedAsync();
        await Assertions.Expect(page.Locator("#signin-password")).ToHaveValueAsync(string.Empty);
        await Assertions.Expect(page.Locator("#signin-password")).ToHaveAttributeAsync("aria-invalid", "true");
        await Assertions.Expect(page.Locator("#signin-user")).ToHaveValueAsync("anna");
    }

    [Theory]
    [InlineData(SupportedCultures.German, "300", "Zu viele Fehlversuche. Das Konto ist gesperrt. Bitte versuche es in 5 Minuten wieder.")]
    [InlineData(SupportedCultures.English, "300", "Too many failed attempts. The account is locked. Please try again in 5 minutes.")]
    [InlineData(SupportedCultures.German, "30", "Bitte versuche es in 1 Minute wieder.")]
    public async Task Lockout_IsAnnounced_WithTheWaitingTime(string culture, string retryAfter, string expected)
    {
        var fake = new FakeAccounts { LoginStatus = 423, RetryAfterSeconds = retryAfter };
        await using var session = await OpenSignedOutAsync("/anmelden", fake, culture);
        var page = session.Page;

        await page.Locator("#signin-user").FillAsync("anna");
        await page.Locator("#signin-password").FillAsync("egal");
        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(page.Locator("main [role=alert]")).ToContainTextAsync(expected);
        await Assertions.Expect(page.Locator("#signin-password")).ToBeFocusedAsync();
    }

    [Fact]
    public async Task EmptySubmit_NamesTheMissingField_AndFocusesIt()
    {
        var fake = new FakeAccounts();
        await using var session = await OpenSignedOutAsync("/anmelden", fake);
        var page = session.Page;

        await page.Locator("#signin-user").FocusAsync();
        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(page.Locator("main [role=alert]")).ToHaveTextAsync("Fehler: Bitte fülle das Feld Benutzername aus.");
        await Assertions.Expect(page.Locator("#signin-user")).ToBeFocusedAsync();
        Assert.False(fake.Mocks.WasCalled("POST", "/api/auth/login"));
    }

    [Fact]
    public async Task FirstTab_StillReachesTheSkipLink_OnTheSignInPage()
    {
        await using var session = await OpenSignedOutAsync("/anmelden", new FakeAccounts());
        var page = session.Page;

        await page.Keyboard.PressAsync("Tab");

        Assert.Contains("skip-link", (await FocusState.ReadAsync(page)).ClassName, StringComparison.Ordinal);
        await Assertions.Expect(page.GetByRole(AriaRole.Navigation)).ToHaveCountAsync(0);
    }
}
