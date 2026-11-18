using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NoMorePaper.Contracts.Accounts;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Accounts;

/// <summary>The pages reached by a link or the emergency code: invitation, password reset, emergency reset.</summary>
public sealed class LinkPagesTests(ServerFixture server)
{
    private const string NewPassword = "Ein-neues-Passwort";

    private Task<BrowserSession> OpenAsync(string path, FakeAccounts fake, Func<IPage, Task> mocks)
        => server.OpenAsync(path, SupportedCultures.German, signedIn: false, async page =>
        {
            await fake.InstallAsync(page);
            await mocks(page);
        });

    [Fact]
    public async Task InvalidInvitation_SaysSo_InAnAlert_WithoutAForm()
    {
        var fake = new FakeAccounts();
        await using var session = await OpenAsync("/einladung/kaputt", fake,
            page => fake.Mocks.RespondAsync(page, "GET", "/api/invitations/kaputt", 404));
        var page = session.Page;

        await Assertions.Expect(page.Locator("main [role=alert]")).ToContainTextAsync("ungültig, schon benutzt oder abgelaufen");
        await Assertions.Expect(page.Locator("form")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task ValidInvitation_CreatesTheAccount_AndSignsIn()
    {
        var fake = new FakeAccounts();
        await using var session = await OpenAsync("/einladung/gut", fake, async page =>
        {
            await fake.Mocks.RespondAsync(page, "GET", "/api/invitations/gut", 200, new InvitationStatusDto(DateTimeOffset.UtcNow.AddDays(6)));
            await fake.Mocks.RespondAsync(page, "POST", "/api/invitations/gut/accept", 204, onCall: () => { });
        });
        var page = session.Page;

        await Assertions.Expect(page.Locator("main")).ToContainTextAsync("Die Einladung gilt bis");
        await Assertions.Expect(page.Locator("#invite-user")).ToHaveAttributeAsync("autocomplete", "username");
        await Assertions.Expect(page.Locator("#invite-password")).ToHaveAttributeAsync("autocomplete", "new-password");
        await page.Locator("#invite-display").FillAsync("Ben Beispiel");
        await page.Locator("#invite-user").FillAsync("ben");
        await page.Locator("#invite-password").FillAsync(NewPassword);
        await page.Locator("#invite-repeat").FillAsync(NewPassword);
        // The server signs the new member in with the answer; the mock does so for the next "who am I".
        await page.RouteAsync(new Regex("/api/auth/me$"), route => route.FulfillAsync(new()
        {
            Status = 200,
            ContentType = "application/json",
            Body = """{"id":"00000000-0000-0000-0000-000000000043","userName":"ben","displayName":"Ben Beispiel","isAdmin":false}""",
        }));
        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(page.Locator(".account-name")).ToContainTextAsync("Ben Beispiel");
        var body = fake.Mocks.BodyOf("POST", "/api/invitations/gut/accept");
        Assert.Contains("\"userName\":\"ben\"", body, StringComparison.Ordinal);
        Assert.Contains("\"displayName\":\"Ben Beispiel\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResetPassword_MismatchIsAnnounced_ThenSuccessReplacesTheForm_AndTakesFocus()
    {
        var fake = new FakeAccounts();
        await using var session = await OpenAsync("/passwort/abc", fake,
            page => fake.Mocks.RespondAsync(page, "POST", "/api/password-reset/abc", 204));
        var page = session.Page;

        await page.Locator("#reset-password").FillAsync(NewPassword);
        await page.Locator("#reset-repeat").FillAsync(NewPassword + "x");
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(page.Locator("main [role=alert]")).ToHaveTextAsync("Fehler: Die beiden Passwörter sind nicht gleich.");
        await Assertions.Expect(page.Locator("#reset-repeat")).ToBeFocusedAsync();

        await page.Locator("#reset-repeat").FillAsync(NewPassword);
        await page.Keyboard.PressAsync("Enter");

        var done = page.Locator("main [role=status]");
        await Assertions.Expect(done).ToContainTextAsync("Das Passwort wurde geändert.");
        await Assertions.Expect(done).ToBeFocusedAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Zur Anmeldung" })).ToBeVisibleAsync();
        Assert.Contains(NewPassword, fake.Mocks.BodyOf("POST", "/api/password-reset/abc"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResetPassword_WithAnUsedLink_SaysTheLinkIsInvalid()
    {
        var fake = new FakeAccounts();
        await using var session = await OpenAsync("/passwort/alt", fake,
            page => fake.Mocks.RespondAsync(page, "POST", "/api/password-reset/alt", 404));
        var page = session.Page;

        await page.Locator("#reset-password").FillAsync(NewPassword);
        await page.Locator("#reset-repeat").FillAsync(NewPassword);
        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(page.Locator("main [role=alert]")).ToContainTextAsync("ungültig, schon benutzt oder abgelaufen");
    }

    [Fact]
    public async Task EmergencyReset_ShowsTheNewCodeOnce_AndRejectsAWrongCode()
    {
        var fake = new FakeAccounts();
        await using var session = await OpenAsync("/notfall", fake,
            page => fake.Mocks.RespondAsync(page, "POST", "/api/emergency-reset", 401));
        var page = session.Page;
        await page.Locator("#emergency-user").FillAsync("anna");
        await page.Locator("#emergency-input").FillAsync("FALSCH");
        await page.Locator("#emergency-password").FillAsync(NewPassword);
        await page.Locator("#emergency-repeat").FillAsync(NewPassword);

        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(page.Locator("main [role=alert]")).ToHaveTextAsync("Fehler: Benutzername oder Notfall-Code stimmen nicht.");
        await Assertions.Expect(page.Locator("#emergency-input")).ToBeFocusedAsync();
        await Assertions.Expect(page.Locator("#emergency-input")).ToHaveValueAsync(string.Empty);
    }

    [Fact]
    public async Task EmergencyReset_Success_ShowsTheNewCode()
    {
        var fake = new FakeAccounts();
        await using var session = await OpenAsync("/notfall", fake,
            page => fake.Mocks.RespondAsync(page, "POST", "/api/emergency-reset", 200, new EmergencyCodeDto("NEUERCODE1234567890A")));
        var page = session.Page;
        await page.Locator("#emergency-user").FillAsync("anna");
        await page.Locator("#emergency-input").FillAsync("ABCDEFGHIJKLMNOPQRST");
        await page.Locator("#emergency-password").FillAsync(NewPassword);
        await page.Locator("#emergency-repeat").FillAsync(NewPassword);

        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(page.Locator("#emergency-code")).ToHaveValueAsync("NEUERCODE1234567890A");
        await page.GetByLabel("Ich habe den Notfall-Code aufbewahrt.").CheckAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Weiter" }).ClickAsync();
        await Assertions.Expect(page).ToHaveURLAsync(new Regex("/anmelden$"));
    }
}
