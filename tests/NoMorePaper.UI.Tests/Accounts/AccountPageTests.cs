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

    private Task<BrowserSession> OpenAsync(
        FakeAccounts fake, int passwordStatus = 204, object? passwordAnswer = null,
        string culture = SupportedCultures.German, IDictionary<string, string>? passwordHeaders = null)
        => server.OpenAsync("/konto", culture, signedIn: true, async page =>
        {
            await fake.InstallAsync(page, signedIn: true);
            await fake.Mocks.RespondAsync(page, "POST", "/api/account/password", passwordStatus, passwordAnswer, passwordHeaders);
        });

    // The validation problem the server sends for refused input.
    private static object Refused(string field, string word) =>
        new { status = 400, errors = new Dictionary<string, string[]> { [field] = [word] } };

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
    public async Task SectionHeadings_FollowTheReadingOrder()
    {
        await using var session = await OpenAsync(new FakeAccounts());
        var headings = session.Page.GetByRole(AriaRole.Heading, new() { Level = 2 });

        await Assertions.Expect(headings).ToHaveTextAsync(["Passwort ändern", "Abmelden", "Eingänge und neue Dokumente"]);
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

        await Assertions.Expect(page.Locator("section[aria-labelledby=password-heading] [role=status]")).ToHaveTextAsync("Das Passwort wurde geändert.");
        await Assertions.Expect(page.Locator("#account-current")).ToHaveValueAsync(string.Empty);
        Assert.Contains("\"newPassword\":\"" + New + "\"", fake.Mocks.BodyOf("POST", "/api/account/password"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_IsAnnounced_AndFocusesIt()
    {
        await using var session = await OpenAsync(new FakeAccounts(), 400, Refused("currentPassword", "wrong"));
        var page = session.Page;

        await FillAsync(page, "falsch", New, New);

        await Assertions.Expect(page.Locator("main .form-error").First).ToHaveTextAsync("Fehler: Das aktuelle Passwort stimmt nicht.");
        await Assertions.Expect(page.Locator("#account-current")).ToBeFocusedAsync();
    }

    [Fact]
    public async Task ChangePassword_NewPasswordRefusedByTheServer_FocusesTheNewPassword()
    {
        await using var session = await OpenAsync(new FakeAccounts(), 400, Refused("newPassword", "too-short"));
        var page = session.Page;

        await FillAsync(page, Current, New, New);

        await Assertions.Expect(page.Locator("main .form-error").First).ToHaveTextAsync("Fehler: Das Passwort braucht mindestens 10 Zeichen.");
        await Assertions.Expect(page.Locator("#account-new")).ToBeFocusedAsync();
    }

    // Wrong current passwords count towards the same lock as sign-in; the server then answers 423.
    [Theory]
    [InlineData(SupportedCultures.German, "300", "Fehler: Zu viele Fehlversuche. Das Konto ist gesperrt. Bitte versuche es in 5 Minuten wieder.")]
    [InlineData(SupportedCultures.English, "300", "Error: Too many failed attempts. The account is locked. Please try again in 5 minutes.")]
    [InlineData(SupportedCultures.German, "30", "Fehler: Zu viele Fehlversuche. Das Konto ist gesperrt. Bitte versuche es in 1 Minute wieder.")]
    public async Task ChangePassword_WhileLocked_AnnouncesTheWaitingTime_AndFocusesTheCurrentPassword(
        string culture, string retryAfter, string expected)
    {
        var headers = new Dictionary<string, string> { ["Retry-After"] = retryAfter };
        await using var session = await OpenAsync(new FakeAccounts(), 423, null, culture, headers);
        var page = session.Page;

        await FillAsync(page, Current, New, New);

        await Assertions.Expect(page.Locator("main [role=alert].form-error").First).ToHaveTextAsync(expected);
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
