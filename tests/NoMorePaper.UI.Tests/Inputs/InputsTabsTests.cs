using Microsoft.Playwright;
using NoMorePaper.Contracts.Inputs;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Inputs;

/// <summary>Tabs, start tab, path and cards of "Eingänge".</summary>
public sealed class InputsTabsTests(ServerFixture server)
{
    private static readonly DateTimeOffset s_now = DateTimeOffset.UtcNow;

    private static InputStatusDto Row(string kind, string? account = null, string? problem = null, bool attention = false, long total = 0) => new(
        kind, account, problem is null ? "ok" : "problem", s_now - TimeSpan.FromMinutes(3),
        problem is null ? s_now - TimeSpan.FromMinutes(3) : null, problem, attention, total);

    private static FakeInputs Admin() => new FakeInputs
    {
        Rows = { Row("scanner-folder"), Row("mail", problem: "unreachable"), Row("icloud", "Anna Beispiel", "code-needed", attention: true) },
    };

    [Fact]
    public async Task TheTabNames_CarryTheStateInWords()
    {
        await using var session = await Admin().OpenAsync(server, "/eingaenge");
        var page = session.Page;

        await Assertions.Expect(page.GetByRole(AriaRole.Tab)).ToHaveCountAsync(3);
        await Assertions.Expect(page.GetByRole(AriaRole.Tab, new() { Name = "Scanner-Ordner läuft" })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Tab, new() { Name = "Mail 1 Problem" })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Tab, new() { Name = "iCloud Drive 1 Problem" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Arrows_HomeAndEnd_MoveAndSelectTheTab_AndShowItsPanel()
    {
        await using var session = await Admin().OpenAsync(server, "/eingaenge");
        var page = session.Page;
        var scanner = page.GetByRole(AriaRole.Tab, new() { Name = "Scanner-Ordner" });
        var mail = page.GetByRole(AriaRole.Tab, new() { Name = "Mail" });
        await scanner.FocusAsync();

        await page.Keyboard.PressAsync("ArrowRight");

        await Assertions.Expect(mail).ToHaveAttributeAsync("aria-selected", "true");
        await Assertions.Expect(mail).ToBeFocusedAsync();
        await Assertions.Expect(page.Locator("#inputs-mail-panel")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#inputs-scanner-folder-panel")).ToBeHiddenAsync();

        await page.Keyboard.PressAsync("End");
        await Assertions.Expect(page.GetByRole(AriaRole.Tab, new() { Name = "iCloud Drive" })).ToHaveAttributeAsync("aria-selected", "true");

        await page.Keyboard.PressAsync("Home");
        await Assertions.Expect(scanner).ToHaveAttributeAsync("aria-selected", "true");
    }

    [Fact]
    public async Task TheQuery_ChoosesTheStartTab_AndNonsenseFallsBackToTheScannerFolder()
    {
        await using (var session = await Admin().OpenAsync(server, "/eingaenge?quelle=mail"))
        {
            await Assertions.Expect(session.Page.GetByRole(AriaRole.Tab, new() { Name = "Mail" })).ToHaveAttributeAsync("aria-selected", "true");
        }

        await using (var session = await Admin().OpenAsync(server, "/eingaenge?quelle=unsinn"))
        {
            await Assertions.Expect(session.Page.GetByRole(AriaRole.Tab, new() { Name = "Scanner-Ordner" })).ToHaveAttributeAsync("aria-selected", "true");
        }
    }

    [Fact]
    public async Task ThePath_LinksToTheSettings_AndMarksTheCurrentPage()
    {
        await using var session = await Admin().OpenAsync(server, "/eingaenge");
        var path = session.Page.Locator("nav[aria-label=Pfad]");

        await Assertions.Expect(path.GetByRole(AriaRole.Link, new() { Name = "Einstellungen" })).ToHaveAttributeAsync("href", "einstellungen");
        await Assertions.Expect(path.Locator("[aria-current=page]")).ToHaveTextAsync("Eingänge");
    }

    [Fact]
    public async Task TheCard_ShowsTheNumberOfDocuments_InOneAndMany()
    {
        var fake = Admin();
        fake.Rows[0] = Row("scanner-folder", total: 128);
        fake.Rows[1] = Row("mail", total: 1);
        await using var session = await fake.OpenAsync(server, "/eingaenge");
        var page = session.Page;

        await Assertions.Expect(page.Locator("#inputs-scanner-folder-panel")).ToContainTextAsync("128 Dokumente");
        await page.GetByRole(AriaRole.Tab, new() { Name = "Mail" }).ClickAsync();
        await Assertions.Expect(page.Locator("#inputs-mail-panel dd").Last).ToHaveTextAsync("1 Dokument");
    }

    [Fact]
    public async Task NotConfiguredMail_ShowsInTheTabAndTheBadge_AndOffersToSetItUp()
    {
        var fake = new FakeInputs { Rows = { Row("mail", problem: "not-configured") } };
        await using var session = await fake.OpenAsync(server, "/eingaenge?quelle=mail");
        var page = session.Page;

        await Assertions.Expect(page.GetByRole(AriaRole.Tab, new() { Name = "Mail nicht eingerichtet" })).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#inputs-mail-panel")).ToContainTextAsync("Nicht eingerichtet");
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Mail einrichten" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task InEnglish_TheTabNamesAndTheTimesAreEnglish()
    {
        await using var session = await Admin().OpenAsync(server, "/eingaenge", SupportedCultures.English);
        var page = session.Page;

        await Assertions.Expect(page.GetByRole(AriaRole.Tab, new() { Name = "Mail 1 problem" })).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#inputs-scanner-folder-panel")).ToContainTextAsync("Last successful run");
    }
}
