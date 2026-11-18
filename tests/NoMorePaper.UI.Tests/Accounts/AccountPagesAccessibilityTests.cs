using Microsoft.Playwright;
using NoMorePaper.Contracts.Accounts;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Accounts;

/// <summary>axe-core finds no violations on any account page, in both languages, in every visible state.</summary>
public sealed class AccountPagesAccessibilityTests(ServerFixture server)
{
    private static readonly MemberDto[] _members =
    [
        new(Guid.NewGuid(), "anna", "Anna Beispiel", true),
        new(Guid.NewGuid(), "ben", "Ben Beispiel", false),
    ];

    private static readonly string[] _signedOutPaths = ["/anmelden", "/einrichten", "/einladung/abc", "/passwort/abc", "/notfall"];

    public static TheoryData<string, string> SignedOutPages()
    {
        var data = new TheoryData<string, string>();
        foreach (var path in _signedOutPaths)
        {
            foreach (var culture in SupportedCultures.All)
            {
                data.Add(path, culture);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(SignedOutPages))]
    public async Task SignedOutPage_HasNoAxeViolations(string path, string culture)
    {
        var fake = new FakeAccounts { SetupNeeded = path == "/einrichten" };
        await using var session = await server.OpenAsync(path, culture, signedIn: false, async page =>
        {
            await fake.InstallAsync(page);
            await fake.Mocks.RespondAsync(page, "GET", "/api/invitations/abc", 200, new InvitationStatusDto(DateTimeOffset.UtcNow.AddDays(7)));
        });
        await session.Page.Locator("main [data-ready]").First.WaitForAsync();

        await AxeCheck.NoViolationsAsync(session.Page);
    }

    [Theory]
    [InlineData(SupportedCultures.German)]
    [InlineData(SupportedCultures.English)]
    public async Task InvalidInvitation_HasNoAxeViolations(string culture)
    {
        var fake = new FakeAccounts();
        await using var session = await server.OpenAsync("/einladung/kaputt", culture, signedIn: false, async page =>
        {
            await fake.InstallAsync(page);
            await fake.Mocks.RespondAsync(page, "GET", "/api/invitations/kaputt", 404);
        });
        await session.Page.Locator("main [role=alert]").WaitForAsync();

        await AxeCheck.NoViolationsAsync(session.Page);
    }

    [Theory]
    [InlineData("/konto", SupportedCultures.German)]
    [InlineData("/konto", SupportedCultures.English)]
    [InlineData("/mitglieder", SupportedCultures.German)]
    [InlineData("/mitglieder", SupportedCultures.English)]
    public async Task SignedInPage_HasNoAxeViolations(string path, string culture)
    {
        var mocks = new AccountMocks();
        await using var session = await server.OpenAsync(path, culture,
            prepare: page => mocks.RespondAsync(page, "GET", "/api/members", 200, _members));
        await session.Page.Locator("main [data-ready]").First.WaitForAsync();

        await AxeCheck.NoViolationsAsync(session.Page);
    }

    [Theory]
    [InlineData(SupportedCultures.German)]
    [InlineData(SupportedCultures.English)]
    public async Task MembersPage_WithLinksAndConfirmation_HasNoAxeViolations(string culture)
    {
        var mocks = new AccountMocks();
        await using var session = await server.OpenAsync("/mitglieder", culture, prepare: async page =>
        {
            await mocks.RespondAsync(page, "GET", "/api/members", 200, _members);
            await mocks.RespondAsync(page, "POST", "/api/invitations", 200,
                new InvitationLinkDto("/einladung/xyz", DateTimeOffset.UtcNow.AddDays(7)));
            await mocks.RespondAsync(page, "POST", $"/api/members/{_members[1].Id}/reset-link", 200,
                new InvitationLinkDto("/passwort/xyz", DateTimeOffset.UtcNow.AddHours(24)));
        });
        var page = session.Page;
        await page.Locator("main table").WaitForAsync();

        await page.Locator(".nmp-button").Filter(new() { HasTextRegex = new("Neues Mitglied einladen|Invite a new member") }).ClickAsync();
        await page.Locator("#invite-link").WaitForAsync();
        await page.Locator("table .nmp-button").First.ClickAsync();
        await page.Locator("#reset-link").WaitForAsync();
        await page.Locator("table .nmp-button").Nth(1).ClickAsync();
        await page.Locator(".confirm-box").WaitForAsync();

        await AxeCheck.NoViolationsAsync(page);
    }

    [Theory]
    [InlineData(SupportedCultures.German)]
    [InlineData(SupportedCultures.English)]
    public async Task SetupEmergencyCode_HasNoAxeViolations(string culture)
    {
        var fake = new FakeAccounts { SetupNeeded = true };
        await using var session = await server.OpenAsync("/einrichten", culture, signedIn: false, async page =>
        {
            await fake.InstallAsync(page);
            await fake.Mocks.RespondAsync(page, "POST", "/api/setup", 200, new EmergencyCodeDto("ABCDEFGHIJKLMNOPQRST"));
        });
        await SetupFlow.FillAndSubmitAsync(session.Page);
        await session.Page.Locator("#emergency-code").WaitForAsync();

        await AxeCheck.NoViolationsAsync(session.Page);
    }

    [Theory]
    [InlineData(SupportedCultures.German)]
    [InlineData(SupportedCultures.English)]
    public async Task SignInError_HasNoAxeViolations(string culture)
    {
        var fake = new FakeAccounts { LoginStatus = 401 };
        await using var session = await server.OpenAsync("/anmelden", culture, signedIn: false, page => fake.InstallAsync(page));
        var page = session.Page;
        await page.Locator("#signin-user").FillAsync("anna");
        await page.Locator("#signin-password").FillAsync("falsch");
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(page.Locator("main [role=alert]")).Not.ToBeEmptyAsync();

        await AxeCheck.NoViolationsAsync(page);
    }
}
