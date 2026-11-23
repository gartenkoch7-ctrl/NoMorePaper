using Microsoft.Playwright;
using NoMorePaper.Contracts.Inputs;
using NoMorePaper.UI.Tests.Accounts;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Inputs;

/// <summary>The page "Eingänge": a tab per input, every input's state as text, the way to its settings, a live region for changes.</summary>
public sealed class InputsPageTests(ServerFixture server)
{
    private static readonly DateTimeOffset s_now = DateTimeOffset.UtcNow;

    private static InputStatusDto Row(
        string kind, string? account = null, string? problem = null, bool attention = false, TimeSpan? lastRun = null) => new(
        kind, account, problem is null ? "ok" : "problem", s_now - (lastRun ?? TimeSpan.FromMinutes(3)),
        problem is null ? s_now - (lastRun ?? TimeSpan.FromMinutes(3)) : null, problem, attention);

    private static FakeInputs Admin() => new FakeInputs
    {
        Rows =
        {
            Row("scanner-folder"),
            Row("mail", problem: "unreachable"),
            Row("icloud", "Anna Beispiel", "code-needed", attention: true),
            Row("icloud", "Ben Beispiel"),
        },
    };

    private static Task OpenTabAsync(IPage page, string name) =>
        page.GetByRole(AriaRole.Tab, new() { Name = name }).ClickAsync();

    [Fact]
    public async Task Page_ShowsOneInputPerTab_WithTheStateAsText_AndTheTimesInWords()
    {
        await using var session = await Admin().OpenAsync(server, "/eingaenge");
        var page = session.Page;

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToHaveTextAsync("Eingänge");
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Level = 2 })).ToHaveTextAsync(["Scanner-Ordner"]);
        var scanner = page.GetByRole(AriaRole.Tabpanel, new() { Name = "Scanner-Ordner" });
        await Assertions.Expect(scanner).ToContainTextAsync("Läuft");
        await Assertions.Expect(scanner).ToContainTextAsync("vor 3 Minuten");

        await OpenTabAsync(page, "Mail");
        var mail = page.GetByRole(AriaRole.Tabpanel, new() { Name = "Mail" });
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Level = 2 })).ToHaveTextAsync(["Mail"]);
        await Assertions.Expect(mail).ToContainTextAsync("Problem");
        await Assertions.Expect(mail).ToContainTextAsync("Der Server des Eingangs ist nicht erreichbar.");
        await Assertions.Expect(mail).ToContainTextAsync("Zuletzt erfolgreich noch nie");
        // The fixed rules of the mail input; the spam folder is named there.
        await Assertions.Expect(mail).ToContainTextAsync("NoMorePaper-Spam");

        await OpenTabAsync(page, "iCloud Drive");
        var icloud = page.GetByRole(AriaRole.Tabpanel, new() { Name = "iCloud Drive" });
        await Assertions.Expect(icloud.GetByRole(AriaRole.Heading, new() { Level = 3 })).ToHaveTextAsync(["Anna Beispiel", "Ben Beispiel", "Über diesen Eingang"]);
        await Assertions.Expect(icloud).ToContainTextAsync("Braucht Aufmerksamkeit");
        await Assertions.Expect(icloud).ToContainTextAsync("Apple verlangt einen neuen Code.");
        await Assertions.Expect(page.Locator("nav.side-nav").GetByRole(AriaRole.Link, new() { Name = "Einstellungen", Exact = true })).ToHaveAttributeAsync("aria-current", "page");
    }

    [Fact]
    public async Task Administrator_GetsTheMailSettingsLink_AndICloudSaysItFollows_WithoutLink()
    {
        var fake = Admin();
        fake.Rows.RemoveAll(row => row.Kind == "icloud");
        await using var session = await fake.OpenAsync(server, "/eingaenge?quelle=mail");
        var page = session.Page;

        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Mail-Einstellungen" })).ToHaveAttributeAsync("href", "eingaenge/mail");
        await OpenTabAsync(page, "iCloud Drive");
        var icloud = page.GetByRole(AriaRole.Tabpanel, new() { Name = "iCloud Drive" });
        await Assertions.Expect(icloud).ToContainTextAsync("Noch nicht eingerichtet.");
        // The page /eingaenge/icloud comes with track D; until then no link leads to "not found".
        await Assertions.Expect(page.Locator("#inputs-icloud-action")).ToHaveTextAsync("iCloud folgt – wartet auf den Test mit Apple-ID.");
        await Assertions.Expect(icloud.GetByRole(AriaRole.Link)).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Member_HasNoMailSettings_AndNoICloudLinkYet()
    {
        var fake = new FakeInputs
        {
            Accounts = new FakeAccounts { IsAdmin = false },
            Rows = { Row("scanner-folder"), Row("mail"), Row("icloud", "Anna Beispiel", "code-needed", attention: true) },
        };
        await using var session = await fake.OpenAsync(server, "/eingaenge?quelle=mail");
        var page = session.Page;

        await Assertions.Expect(page.GetByRole(AriaRole.Tabpanel, new() { Name = "Mail" })).ToContainTextAsync("Das Postfach richtet der Verwalter ein.");
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Mail-Einstellungen" })).ToHaveCountAsync(0);
        await OpenTabAsync(page, "iCloud Drive");
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Code erneuern" })).ToHaveCountAsync(0);
        await Assertions.Expect(page.Locator("#inputs-icloud-action")).ToHaveTextAsync("iCloud folgt – wartet auf den Test mit Apple-ID.");
    }

    [Fact]
    public async Task AProblemTheClientDoesNotKnow_GetsAGeneralText()
    {
        var fake = new FakeInputs { Rows = { Row("mail", problem: "something-new") } };
        await using var session = await fake.OpenAsync(server, "/eingaenge?quelle=mail");

        await Assertions.Expect(session.Page.GetByRole(AriaRole.Tabpanel, new() { Name = "Mail" })).ToContainTextAsync("Unbekanntes Problem.");
    }

    [Fact]
    public async Task LoadError_IsShownAsText_AndRetryLoadsAgain()
    {
        var fake = Admin();
        fake.InputsStatus = 500;
        await using var session = await fake.OpenAsync(server, "/eingaenge");
        var page = session.Page;

        await Assertions.Expect(page.Locator("main .nmp-notice-error")).ToContainTextAsync("Die Eingänge konnten nicht geladen werden");
        fake.InputsStatus = 200;
        await page.GetByRole(AriaRole.Button, new() { Name = "Erneut versuchen" }).ClickAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Level = 2 })).ToHaveTextAsync(["Scanner-Ordner"]);
    }

    [Fact]
    public async Task TheSameLoadErrorAgain_AfterRetry_IsAnnouncedInTheLiveRegion()
    {
        var fake = Admin();
        fake.InputsStatus = 500;
        await using var session = await fake.OpenAsync(server, "/eingaenge");
        var page = session.Page;
        await Assertions.Expect(page.Locator("main .nmp-notice-error")).ToContainTextAsync("Die Eingänge konnten nicht geladen werden");
        await Assertions.Expect(page.Locator("main [role=status].live-region")).ToHaveTextAsync(string.Empty);

        await page.Locator("#inputs-retry").FocusAsync();
        await page.Keyboard.PressAsync("Enter");

        // The alert box is unchanged and says nothing again: the status region says it.
        await Assertions.Expect(page.Locator("main [role=status].live-region")).ToContainTextAsync("Die Eingänge konnten nicht geladen werden");
        await Assertions.Expect(page.Locator("#inputs-retry")).ToBeFocusedAsync();
    }

    [Fact]
    public async Task RefreshByKeyboard_SaysWhatChanged_InTheLiveRegion()
    {
        var fake = Admin();
        await using var session = await fake.OpenAsync(server, "/eingaenge?quelle=mail");
        var page = session.Page;
        var refresh = page.GetByRole(AriaRole.Button, new() { Name = "Jetzt aktualisieren" });
        await refresh.FocusAsync();
        fake.Rows[1] = Row("mail");

        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(page.Locator("main .live-region")).ToHaveTextAsync("Die Eingänge haben sich geändert. Mail: Läuft ohne Probleme.");
        await Assertions.Expect(page.GetByRole(AriaRole.Tabpanel, new() { Name = "Mail" })).ToContainTextAsync("Läuft");

        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(page.Locator("main .live-region")).ToHaveTextAsync("Aktualisiert. Nichts hat sich geändert.");
    }

    [Fact]
    public async Task InEnglish_TheTextsAreEnglish()
    {
        await using var session = await Admin().OpenAsync(server, "/eingaenge?quelle=mail", SupportedCultures.English);
        var page = session.Page;

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Level = 2 })).ToHaveTextAsync(["Mail"]);
        var mail = page.GetByRole(AriaRole.Tabpanel, new() { Name = "Mail" });
        await Assertions.Expect(mail).ToContainTextAsync("The input's server cannot be reached.");
        await Assertions.Expect(mail).ToContainTextAsync("3 minutes ago");
    }

    [Theory]
    [InlineData(SupportedCultures.German)]
    [InlineData(SupportedCultures.English)]
    public async Task Page_HasNoAxeViolations_InEveryTab(string culture)
    {
        await using var session = await Admin().OpenAsync(server, "/eingaenge", culture);
        var page = session.Page;
        await page.Locator("main [data-ready]").First.WaitForAsync();

        foreach (var tab in await page.GetByRole(AriaRole.Tab).AllAsync())
        {
            await tab.ClickAsync();
            await AxeCheck.NoViolationsAsync(page);
        }
    }
}
