using Microsoft.Playwright;
using NoMorePaper.Contracts.Inputs;
using NoMorePaper.UI.Tests.Accounts;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Inputs;

/// <summary>The mailbox card and the rule list on <c>/eingaenge/mail</c>.</summary>
public sealed class MailboxCardTests(ServerFixture server)
{
    private const string Path = "/api/inputs/mail";

    private static readonly DateTimeOffset s_now = DateTimeOffset.UtcNow;

    private static readonly MailSettingsDto s_stored = new("imap.haushalt.example", 993, "ssl", "dokumente", "mx.haushalt.example", true);

    private static readonly InputStatusDto[] s_rows =
    [
        new("mail", null, "ok", s_now - TimeSpan.FromMinutes(3), s_now - TimeSpan.FromMinutes(3), null, false, 42),
    ];

    private Task<BrowserSession> OpenAsync(
        AccountMocks mocks, string culture = SupportedCultures.German, MailSettingsDto? stored = null, int statusCode = 200)
        => server.OpenAsync("/eingaenge/mail", culture, prepare: async page =>
        {
            await mocks.RespondAsync(page, "GET", Path, 200, stored ?? s_stored);
            await mocks.RespondAsync(page, "PUT", Path, 204);
            await mocks.RespondAsync(page, "GET", "/api/inputs", statusCode, statusCode == 200 ? s_rows : new { status = statusCode });
        });

    [Fact]
    public async Task TheCard_ShowsTheStoredSignIn_TheTakt_AndTheState()
    {
        await using var session = await OpenAsync(new AccountMocks());
        var card = session.Page.Locator("section.mail-card");

        await Assertions.Expect(card).ToContainTextAsync("dokumente");
        await Assertions.Expect(card).ToContainTextAsync("IMAP · SSL/TLS · imap.haushalt.example:993");
        await Assertions.Expect(card).ToContainTextAsync("alle 5 Minuten");
        await Assertions.Expect(card).ToContainTextAsync("Läuft");
        await Assertions.Expect(card).ToContainTextAsync("Zuletzt gelaufen");
    }

    [Fact]
    public async Task WhenTheStateCannotBeLoaded_TheCardSaysSo_AndTheFormStaysUsable()
    {
        await using var session = await OpenAsync(new AccountMocks(), statusCode: 500);
        var page = session.Page;

        await Assertions.Expect(page.Locator("section.mail-card")).ToContainTextAsync("Der Zustand des Postfachs konnte nicht geladen werden.");
        await Assertions.Expect(page.Locator("#mail-host")).ToHaveValueAsync("imap.haushalt.example");
    }

    [Fact]
    public async Task WithoutAHost_TheCardSaysItIsNotSetUp()
    {
        await using var session = await OpenAsync(new AccountMocks(), stored: s_stored with { Host = string.Empty, HasPassword = false });

        await Assertions.Expect(session.Page.Locator("section.mail-card")).ToContainTextAsync("Nicht eingerichtet");
    }

    [Fact]
    public async Task AfterSaving_TheCardShowsTheNewHost()
    {
        await using var session = await OpenAsync(new AccountMocks());
        var page = session.Page;

        await page.Locator("#mail-host").FillAsync("imap.neu.example");
        await page.Locator("#mail-password").FillAsync("geheim");
        await page.GetByRole(AriaRole.Button, new() { Name = "Speichern" }).ClickAsync();

        await Assertions.Expect(page.Locator("section.mail-card")).ToContainTextAsync("imap.neu.example:993");
    }

    [Fact]
    public async Task FirstSetUp_ReplacesTheNotSetUpState_WithoutAStaleRow()
    {
        var rows = new[] { new InputStatusDto("mail", null, "problem", s_now, null, "not-configured", false) };
        await using var session = await server.OpenAsync("/eingaenge/mail", SupportedCultures.German, prepare: async page =>
        {
            var mocks = new AccountMocks();
            await mocks.RespondAsync(page, "GET", Path, 200, s_stored with { Host = string.Empty, HasPassword = false });
            await mocks.RespondAsync(page, "PUT", Path, 204);
            await mocks.RespondAsync(page, "GET", "/api/inputs", 200, rows);
        });
        var page = session.Page;
        var card = page.Locator("section.mail-card");
        await Assertions.Expect(card).ToContainTextAsync("Nicht eingerichtet");

        await page.Locator("#mail-host").FillAsync("imap.neu.example");
        await page.Locator("#mail-user").FillAsync("dokumente");
        await page.Locator("#mail-password").FillAsync("geheim");
        await page.GetByRole(AriaRole.Button, new() { Name = "Speichern" }).ClickAsync();

        await Assertions.Expect(card).ToContainTextAsync("imap.neu.example:993");
        await Assertions.Expect(card).ToContainTextAsync("Noch nicht gelaufen");
        await Assertions.Expect(card).Not.ToContainTextAsync("Nicht eingerichtet");
    }

    [Fact]
    public async Task TheRuleList_HasFiveRules_UnderItsOwnHeading()
    {
        await using var session = await OpenAsync(new AccountMocks());
        var page = session.Page;

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "So behandelt NoMorePaper Mails", Level = 2 })).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".mail-rules-list").GetByRole(AriaRole.Listitem)).ToHaveCountAsync(5);
    }

    [Fact]
    public async Task ThePath_LeadsBackToTheMailTab()
    {
        await using var session = await OpenAsync(new AccountMocks());

        await Assertions.Expect(session.Page.Locator("nav[aria-label=Pfad]").GetByRole(AriaRole.Link, new() { Name = "Eingänge" }))
            .ToHaveAttributeAsync("href", "eingaenge?quelle=mail");
    }

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
    public async Task InEnglish_TheRulesHeadingIsEnglish()
    {
        await using var session = await OpenAsync(new AccountMocks(), SupportedCultures.English);

        await Assertions.Expect(session.Page.GetByRole(AriaRole.Heading, new() { Name = "How NoMorePaper handles mail" })).ToBeVisibleAsync();
    }
}
