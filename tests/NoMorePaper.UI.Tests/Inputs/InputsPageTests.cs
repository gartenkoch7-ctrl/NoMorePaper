using Microsoft.Playwright;
using NoMorePaper.Contracts.Inputs;
using NoMorePaper.UI.Tests.Accounts;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Inputs;

/// <summary>The page "Eingänge": every input's state as text, the way to its settings, a live region for changes.</summary>
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

    [Fact]
    public async Task Page_ShowsEveryInputAsAnSection_WithTheStateAsText_AndTheTimesInWords()
    {
        await using var session = await Admin().OpenAsync(server, "/eingaenge");
        var page = session.Page;

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToHaveTextAsync("Eingänge");
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Level = 2 })).ToHaveTextAsync(["Scanner-Ordner", "Mail", "iCloud Drive"]);
        var scanner = page.GetByRole(AriaRole.Region, new() { Name = "Scanner-Ordner" });
        await Assertions.Expect(scanner).ToContainTextAsync("Läuft ohne Probleme.");
        await Assertions.Expect(scanner).ToContainTextAsync("vor 3 Minuten");
        var mail = page.GetByRole(AriaRole.Region, new() { Name = "Mail" });
        await Assertions.Expect(mail).ToContainTextAsync("Problem: Der Server des Eingangs ist nicht erreichbar.");
        await Assertions.Expect(mail).ToContainTextAsync("Letzter Erfolg noch nie");
        await Assertions.Expect(mail).ToContainTextAsync("NoMorePaper-Spam");
        var icloud = page.GetByRole(AriaRole.Region, new() { Name = "iCloud Drive" });
        await Assertions.Expect(icloud.GetByRole(AriaRole.Heading, new() { Level = 3 })).ToHaveTextAsync(["Anna Beispiel", "Ben Beispiel"]);
        await Assertions.Expect(icloud).ToContainTextAsync("Problem: Apple verlangt einen neuen Code. Das braucht deine Aufmerksamkeit.");
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Eingänge" })).ToHaveAttributeAsync("aria-current", "page");
    }

    [Fact]
    public async Task Administrator_GetsTheMailSettingsLink_AndTheICloudLink()
    {
        var fake = Admin();
        fake.Rows.RemoveAll(row => row.Kind == "icloud");
        await using var session = await fake.OpenAsync(server, "/eingaenge");
        var page = session.Page;

        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Mail-Einstellungen" })).ToHaveAttributeAsync("href", "eingaenge/mail");
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "iCloud einrichten" })).ToHaveAttributeAsync("href", "eingaenge/icloud");
        await Assertions.Expect(page.GetByRole(AriaRole.Region, new() { Name = "iCloud Drive" })).ToContainTextAsync("Noch nicht eingerichtet.");
    }

    [Fact]
    public async Task Member_HasNoMailSettings_ButCanRenewItsOwnICloudCode()
    {
        var fake = new FakeInputs
        {
            Accounts = new FakeAccounts { IsAdmin = false },
            Rows = { Row("scanner-folder"), Row("mail"), Row("icloud", "Anna Beispiel", "code-needed", attention: true) },
        };
        await using var session = await fake.OpenAsync(server, "/eingaenge");
        var page = session.Page;

        await Assertions.Expect(page.GetByRole(AriaRole.Region, new() { Name = "Mail" })).ToContainTextAsync("Das Postfach richtet der Verwalter ein.");
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Mail-Einstellungen" })).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Code erneuern" })).ToHaveAttributeAsync("href", "eingaenge/icloud");
    }

    [Fact]
    public async Task NotConfiguredMail_OffersToSetItUp()
    {
        var fake = new FakeInputs { Rows = { Row("mail", problem: "not-configured") } };
        await using var session = await fake.OpenAsync(server, "/eingaenge");

        await Assertions.Expect(session.Page.GetByRole(AriaRole.Link, new() { Name = "Mail einrichten" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task AProblemTheClientDoesNotKnow_GetsAGeneralText()
    {
        var fake = new FakeInputs { Rows = { Row("mail", problem: "something-new") } };
        await using var session = await fake.OpenAsync(server, "/eingaenge");

        await Assertions.Expect(session.Page.GetByRole(AriaRole.Region, new() { Name = "Mail" })).ToContainTextAsync("Problem: Unbekanntes Problem.");
    }

    [Fact]
    public async Task LoadError_IsShownAsText_AndRetryLoadsAgain()
    {
        var fake = Admin();
        fake.InputsStatus = 500;
        await using var session = await fake.OpenAsync(server, "/eingaenge");
        var page = session.Page;

        await Assertions.Expect(page.Locator("main .error-box")).ToContainTextAsync("Die Eingänge konnten nicht geladen werden");
        fake.InputsStatus = 200;
        await page.GetByRole(AriaRole.Button, new() { Name = "Erneut versuchen" }).ClickAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Level = 2 })).ToHaveTextAsync(["Scanner-Ordner", "Mail", "iCloud Drive"]);
    }

    [Fact]
    public async Task RefreshByKeyboard_SaysWhatChanged_InTheLiveRegion()
    {
        var fake = Admin();
        await using var session = await fake.OpenAsync(server, "/eingaenge");
        var page = session.Page;
        var refresh = page.GetByRole(AriaRole.Button, new() { Name = "Jetzt aktualisieren" });
        await refresh.FocusAsync();
        fake.Rows[1] = Row("mail");

        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(page.Locator("main .live-region")).ToHaveTextAsync("Die Eingänge haben sich geändert. Mail: Läuft ohne Probleme.");
        await Assertions.Expect(page.GetByRole(AriaRole.Region, new() { Name = "Mail" })).ToContainTextAsync("Läuft ohne Probleme.");

        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(page.Locator("main .live-region")).ToHaveTextAsync("Aktualisiert. Nichts hat sich geändert.");
    }

    [Fact]
    public async Task InEnglish_TheTextsAreEnglish()
    {
        await using var session = await Admin().OpenAsync(server, "/eingaenge", SupportedCultures.English);
        var page = session.Page;

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Level = 2 })).ToHaveTextAsync(["Scanner folder", "Mail", "iCloud Drive"]);
        await Assertions.Expect(page.GetByRole(AriaRole.Region, new() { Name = "Mail" })).ToContainTextAsync("Problem: The input's server cannot be reached.");
        await Assertions.Expect(page.GetByRole(AriaRole.Region, new() { Name = "Mail" })).ToContainTextAsync("3 minutes ago");
    }

    [Theory]
    [InlineData(SupportedCultures.German)]
    [InlineData(SupportedCultures.English)]
    public async Task Page_HasNoAxeViolations(string culture)
    {
        await using var session = await Admin().OpenAsync(server, "/eingaenge", culture);
        await session.Page.Locator("main [data-ready]").First.WaitForAsync();

        await AxeCheck.NoViolationsAsync(session.Page);
    }
}
