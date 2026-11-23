using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NoMorePaper.Contracts.Accounts;
using NoMorePaper.UI.Tests.Documents;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.UI.Tests.Start;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Accounts;

/// <summary>First-run setup: the redirect, validation, and the emergency code that is shown once.</summary>
public sealed class SetupTests(ServerFixture server)
{
    private const string Code = "ABCDEFGHIJKLMNOPQRST";

    private Task<BrowserSession> OpenAsync(string path, FakeAccounts fake, string culture = SupportedCultures.German)
        => server.OpenAsync(path, culture, signedIn: false, async page =>
        {
            await fake.InstallAsync(page);
            await fake.Mocks.RespondAsync(page, "POST", "/api/setup", 200, new EmergencyCodeDto(Code), onCall: fake.MarkSignedIn);
            await StartPageMocks.InstallAsync(page);
        });

    [Fact]
    public async Task WhenSetupIsNeeded_EveryPageGoesToSetup()
    {
        await using var session = await OpenAsync("/dokumente", new FakeAccounts { SetupNeeded = true });

        await Assertions.Expect(session.Page).ToHaveURLAsync(new Regex("/einrichten$"));
        await Assertions.Expect(session.Page.Locator("h1")).ToHaveTextAsync("Haushalt einrichten");
    }

    [Fact]
    public async Task Fields_HaveLabels_AndAutocomplete()
    {
        await using var session = await OpenAsync("/einrichten", new FakeAccounts { SetupNeeded = true });
        var page = session.Page;

        await Assertions.Expect(page.Locator("#setup-display")).ToHaveAttributeAsync("autocomplete", "name");
        await Assertions.Expect(page.Locator("#setup-user")).ToHaveAttributeAsync("autocomplete", "username");
        await Assertions.Expect(page.Locator("#setup-password")).ToHaveAttributeAsync("autocomplete", "new-password");
        await Assertions.Expect(page.Locator("#setup-repeat")).ToHaveAttributeAsync("autocomplete", "new-password");
        await Assertions.Expect(page.GetByLabel("Neues Passwort", new() { Exact = true })).ToBeVisibleAsync();
        // The length rule is read with the field, not only after a mistake.
        await Assertions.Expect(page.Locator("#setup-password")).ToHaveAttributeAsync("aria-describedby", "setup-password-hint");
        await Assertions.Expect(page.Locator("#setup-password-hint")).ToHaveTextAsync("Mindestens 10 Zeichen.");
    }

    [Fact]
    public async Task ShortPassword_IsAnnounced_AndFocusesThePasswordField()
    {
        await using var session = await OpenAsync("/einrichten", new FakeAccounts { SetupNeeded = true });
        var page = session.Page;

        await SetupFlow.FillAndSubmitAsync(page, "kurz", "kurz");

        await Assertions.Expect(page.Locator("main [role=alert]")).ToHaveTextAsync("Fehler: Das Passwort braucht mindestens 10 Zeichen.");
        await Assertions.Expect(page.Locator("#setup-password")).ToBeFocusedAsync();
    }

    [Fact]
    public async Task DifferentRepeat_IsAnnounced_AndFocusesTheRepeatField()
    {
        await using var session = await OpenAsync("/einrichten", new FakeAccounts { SetupNeeded = true });
        var page = session.Page;

        await SetupFlow.FillAndSubmitAsync(page, SetupFlow.Password, SetupFlow.Password + "x");

        await Assertions.Expect(page.Locator("main [role=alert]")).ToHaveTextAsync("Fehler: Die beiden Passwörter sind nicht gleich.");
        await Assertions.Expect(page.Locator("#setup-repeat")).ToBeFocusedAsync();
    }

    // The server names the refused field with a fixed word; the page says it in its own words.
    [Theory]
    [InlineData("userName", "taken", "#setup-user", "Fehler: Dieser Benutzername ist schon vergeben. Bitte wähle einen anderen.")]
    [InlineData("password", "too-short", "#setup-password", "Fehler: Das Passwort braucht mindestens 10 Zeichen.")]
    public async Task RefusedByTheServer_IsAnnounced_AndFocusesTheNamedField(
        string field, string word, string input, string message)
    {
        var fake = new FakeAccounts { SetupNeeded = true };
        await using var session = await OpenAsync("/einrichten", fake);
        var page = session.Page;
        var errors = new Dictionary<string, string[]> { [field] = [word] };
        await fake.Mocks.RespondAsync(page, "POST", "/api/setup", 400, new { status = 400, errors });

        await SetupFlow.FillAndSubmitAsync(page);

        await Assertions.Expect(page.Locator("main [role=alert]")).ToHaveTextAsync(message);
        await Assertions.Expect(page.Locator(input)).ToBeFocusedAsync();
    }

    [Fact]
    public async Task EmergencyCode_IsShownLarge_Copyable_AndNeedsConfirmation()
    {
        var fake = new FakeAccounts { SetupNeeded = true };
        await using var session = await OpenAsync("/einrichten", fake);
        var page = session.Page;
        await page.AddInitScriptAsync("Object.defineProperty(navigator.clipboard, 'writeText', { value: async t => { window.__copied = t; } });");
        await page.ReloadAsync();
        await page.Locator("h1").WaitForAsync();

        await SetupFlow.FillAndSubmitAsync(page);

        // Focus moves to the new content; the code sits in a read-only field with a copy button.
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Dein Notfall-Code" })).ToBeFocusedAsync();
        await Assertions.Expect(page.Locator("#emergency-code")).ToHaveValueAsync(Code);
        await Assertions.Expect(page.Locator("#emergency-code")).ToHaveAttributeAsync("readonly", string.Empty);
        await page.GetByRole(AriaRole.Button, new() { Name = "Kopieren: Notfall-Code" }).ClickAsync();
        await Assertions.Expect(page.Locator(".copy-field .live-region")).ToHaveTextAsync("Notfall-Code wurde kopiert.");
        Assert.Equal(Code, await page.EvaluateAsync<string>("window.__copied"));

        // Without the confirmation it does not go on.
        await page.GetByRole(AriaRole.Button, new() { Name = "Weiter" }).ClickAsync();
        await Assertions.Expect(page.Locator(".emergency-code [role=alert]"))
            .ToHaveTextAsync("Fehler: Bitte bestätige, dass du den Notfall-Code aufbewahrt hast.");
        await Assertions.Expect(page.GetByLabel("Ich habe den Notfall-Code aufbewahrt.")).ToBeFocusedAsync();

        await page.GetByLabel("Ich habe den Notfall-Code aufbewahrt.").CheckAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Weiter" }).ClickAsync();

        await Assertions.Expect(page.Locator(".account-name")).ToBeVisibleAsync();
        // The setup answer signed the administrator in already; no second sign-in.
        Assert.False(fake.Mocks.WasCalled("POST", "/api/auth/login"));
    }

    [Fact]
    public async Task WhenAlreadySetUp_SaysSoAndLinksToSignIn()
    {
        await using var session = await OpenAsync("/einrichten", new FakeAccounts());
        var page = session.Page;

        await Assertions.Expect(page.Locator("main [data-ready]").First).ToContainTextAsync("schon eingerichtet");
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Zur Anmeldung" })).ToHaveAttributeAsync("href", "anmelden");
    }
}
