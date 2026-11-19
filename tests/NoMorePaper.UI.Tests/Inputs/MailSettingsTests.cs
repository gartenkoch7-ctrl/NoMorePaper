using Microsoft.Playwright;
using NoMorePaper.Contracts.Inputs;
using NoMorePaper.UI.Tests.Accounts;
using NoMorePaper.UI.Tests.Classification;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Inputs;

/// <summary>The mailbox settings (administrator only) and "Aus der Mail" in the viewer.</summary>
public sealed class MailSettingsTests(ServerFixture server)
{
    private const string Path = "/api/inputs/mail";

    private static readonly MailSettingsDto s_stored = new("imap.haushalt.example", 993, "ssl", "dokumente", "mx.haushalt.example", true);

    private Task<BrowserSession> OpenAsync(
        AccountMocks mocks, string culture = SupportedCultures.German, int testStatus = 204, MailSettingsDto? stored = null, object? refusal = null)
        => server.OpenAsync("/eingaenge/mail", culture, prepare: async page =>
        {
            await mocks.RespondAsync(page, "GET", Path, 200, stored ?? s_stored);
            await mocks.RespondAsync(page, "PUT", Path, refusal is null ? 204 : 400, refusal);
            await mocks.RespondAsync(page, "POST", Path + "/test", testStatus, testStatus == 204 ? null : new { status = testStatus });
        });

    [Theory]
    [InlineData(SupportedCultures.German)]
    [InlineData(SupportedCultures.English)]
    public async Task ThePage_HasNoAxeViolations(string culture)
    {
        await using var session = await OpenAsync(new AccountMocks(), culture);
        await session.Page.Locator("main [data-ready]").First.WaitForAsync();

        await AxeCheck.NoViolationsAsync(session.Page);
    }

    [Fact]
    public async Task TheStoredPassword_IsNeverShown_AndAnEmptyFieldKeepsIt()
    {
        var mocks = new AccountMocks();
        await using var session = await OpenAsync(mocks);
        var page = session.Page;

        await Assertions.Expect(page.Locator("#mail-host")).ToHaveValueAsync("imap.haushalt.example");
        await Assertions.Expect(page.Locator("#mail-password")).ToHaveValueAsync(string.Empty);
        await Assertions.Expect(page.Locator("#mail-password-hint")).ToHaveTextAsync("Ein Passwort ist gespeichert. Lass das Feld leer, um es zu behalten.");
        await Assertions.Expect(page.Locator("#mail-password")).ToHaveAttributeAsync("autocomplete", "new-password");

        await page.GetByRole(AriaRole.Button, new() { Name = "Speichern" }).ClickAsync();

        await Assertions.Expect(page.Locator("main [role=status]").First).ToHaveTextAsync("Einstellungen des Postfachs gespeichert.");
        Assert.Contains("\"password\":null", mocks.BodyOf("PUT", Path), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ANewPassword_IsSentOnce_ThenTheFieldIsEmptyAgain()
    {
        var mocks = new AccountMocks();
        await using var session = await OpenAsync(mocks, stored: s_stored with { HasPassword = false });
        var page = session.Page;

        await page.GetByRole(AriaRole.Button, new() { Name = "Speichern" }).ClickAsync();
        await Assertions.Expect(page.Locator("#mail-password")).ToBeFocusedAsync();
        Assert.False(mocks.WasCalled("PUT", Path));

        await page.Locator("#mail-password").FillAsync("postfach-geheim");
        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(page.Locator("#mail-password")).ToHaveValueAsync(string.Empty);
        await Assertions.Expect(page.Locator("#mail-password-hint")).ToHaveTextAsync("Ein Passwort ist gespeichert. Lass das Feld leer, um es zu behalten.");
        Assert.Contains("\"password\":\"postfach-geheim\"", mocks.BodyOf("PUT", Path), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(204, "Verbindung klappt: Anmeldung am Mailserver erfolgreich.")]
    [InlineData(422, "Fehler: Der Mailserver hat die Anmeldung abgelehnt. Prüfe Benutzername und Passwort.")]
    [InlineData(502, "Fehler: Der Mailserver ist nicht erreichbar. Prüfe Server, Port und Verschlüsselung.")]
    public async Task TheTest_SaysItsResultInWords(int status, string expected)
    {
        var mocks = new AccountMocks();
        await using var session = await OpenAsync(mocks, testStatus: status);
        var page = session.Page;

        await page.GetByRole(AriaRole.Button, new() { Name = "Verbindung testen" }).ClickAsync();

        var region = page.Locator(status == 204 ? "main [role=status]" : "main [role=alert]").First;
        await Assertions.Expect(region).ToHaveTextAsync(expected);
        Assert.Contains("\"host\":\"imap.haushalt.example\"", mocks.BodyOf("POST", Path + "/test"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AWrongPort_IsAnnounced_AndFocused()
    {
        await using var session = await OpenAsync(new AccountMocks());
        var page = session.Page;

        await page.Locator("#mail-port").FillAsync("70000");
        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(page.Locator("main [role=alert]").First).ToHaveTextAsync("Fehler: Der Port muss eine Zahl von 1 bis 65535 sein.");
        await Assertions.Expect(page.Locator("#mail-port")).ToBeFocusedAsync();
    }

    [Fact]
    public async Task AnotherServer_NeedsThePasswordAgain_SaidAsText_WithFocusOnTheField()
    {
        var mocks = new AccountMocks();
        await using var session = await OpenAsync(mocks);
        var page = session.Page;

        await page.Locator("#mail-host").FillAsync("imap.anderer.example");
        await Assertions.Expect(page.Locator("#mail-password-hint")).ToHaveTextAsync("Server oder Benutzer wurden geändert: Gib das Passwort neu ein.");
        await page.GetByRole(AriaRole.Button, new() { Name = "Verbindung testen" }).ClickAsync();

        await Assertions.Expect(page.Locator("main [role=alert]").First).ToContainTextAsync("Gib dafür das Passwort neu ein");
        await Assertions.Expect(page.Locator("#mail-password")).ToBeFocusedAsync();
        await Assertions.Expect(page.Locator("#mail-password")).ToHaveAttributeAsync("aria-required", "true");
        Assert.False(mocks.WasCalled("POST", Path + "/test"));
        await Assertions.Expect(page.Locator("#mail-security option")).ToHaveTextAsync(["SSL/TLS", "STARTTLS"]);
    }

    [Theory]
    [InlineData("password", "required", "#mail-password", "Gib dafür das Passwort neu ein")]
    [InlineData("security", "unencrypted", "#mail-security", "Eine Verbindung ohne Verschlüsselung ist nicht erlaubt.")]
    public async Task ARefusalOfTheServer_IsSaidAsText_AndFocusesItsField(string field, string word, string focused, string text)
    {
        var refusal = new { status = 400, errors = new Dictionary<string, string[]> { [field] = [word] } };
        await using var session = await OpenAsync(new AccountMocks(), refusal: refusal);
        var page = session.Page;

        await page.GetByRole(AriaRole.Button, new() { Name = "Speichern" }).ClickAsync();

        await Assertions.Expect(page.Locator("main [role=alert]").First).ToContainTextAsync(text);
        await Assertions.Expect(page.Locator(focused)).ToBeFocusedAsync();
        await Assertions.Expect(page.Locator(focused)).ToHaveAttributeAsync("aria-invalid", "true");
    }

    [Fact]
    public async Task SaveAndTest_KeepTheKeyboardFocusOnTheirButton()
    {
        await using var session = await OpenAsync(new AccountMocks());
        var page = session.Page;
        var save = page.GetByRole(AriaRole.Button, new() { Name = "Speichern" });
        var test = page.GetByRole(AriaRole.Button, new() { Name = "Verbindung testen" });

        await save.FocusAsync();
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(page.Locator("main [role=status]").First).ToHaveTextAsync("Einstellungen des Postfachs gespeichert.");
        await Assertions.Expect(save).ToBeFocusedAsync();

        await test.FocusAsync();
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(page.Locator("main [role=status]").First).ToHaveTextAsync("Verbindung klappt: Anmeldung am Mailserver erfolgreich.");
        await Assertions.Expect(test).ToBeFocusedAsync();
    }

    [Fact]
    public async Task AMember_GetsNoForm()
    {
        var fake = new FakeAccounts { IsAdmin = false };
        await using var session = await server.OpenAsync("/eingaenge/mail", SupportedCultures.German, signedIn: true, page => fake.InstallAsync(page, signedIn: true));

        await Assertions.Expect(session.Page.Locator("main [data-ready]")).ToHaveTextAsync("Nur der Verwalter kann das Postfach des Haushalts einrichten.");
        await Assertions.Expect(session.Page.Locator("#mail-host")).ToHaveCountAsync(0);
        Assert.False(fake.Mocks.WasCalled("GET", Path));
    }

    [Fact]
    public async Task TheViewer_ShowsFromTheMail_RightAfterTheSummary()
    {
        var origin = new MailOriginDto("rechnung@stadtwerke.example", "Ihre Rechnung November", "Zahlung bis 30.11. per Lastschrift.");
        var fake = new FakeFields(Guid.NewGuid(), ClassificationSamples.Automatic() with { Mail = origin, Source = "mail" }, false);
        await using var session = await fake.OpenViewerAsync(server);

        var section = session.Page.GetByRole(AriaRole.Region, new() { Name = "Aus der Mail" });
        await Assertions.Expect(section.GetByRole(AriaRole.Definition)).ToHaveTextAsync(
            ["Zahlung bis 30.11. per Lastschrift.", "rechnung@stadtwerke.example", "Ihre Rechnung November"]);
        var text = await session.Page.GetByRole(AriaRole.Region, new() { Name = "Angaben" }).InnerTextAsync();
        Assert.True(text.IndexOf("Aus der Mail", StringComparison.Ordinal) < text.IndexOf("Dokumentart", StringComparison.Ordinal));
        await AxeCheck.NoViolationsAsync(session.Page);
    }
}
