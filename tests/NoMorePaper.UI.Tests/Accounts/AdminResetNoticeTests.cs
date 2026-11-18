using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NoMorePaper.Contracts.Accounts;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Accounts;

/// <summary>
/// Against the real server: a member whose password was set through the administrator's reset link
/// is told at the next sign-in, on the start page, until the notice is confirmed.
/// </summary>
public sealed class AdminResetNoticeTests(ServerFixture server)
{
    private const string FirstPassword = "Erstes-Passwort-2026";
    private const string ResetPassword = "Zurueckgesetzt-2026";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(SupportedCultures.German, "über einen Rücksetzlink des Verwalters geändert", "Verstanden")]
    [InlineData(SupportedCultures.English, "through a reset link from the administrator", "Got it")]
    public async Task AfterAReset_TheNoticeIsAnnouncedAndFocused_UntilConfirmed(string culture, string text, string button)
    {
        var userName = "reset" + Guid.NewGuid().ToString("N")[..8];
        await ResetByTheAdministratorAsync(userName);
        await using var session = await server.OpenAsync("/anmelden?ziel=%2Fdokumente", culture, signedIn: false);
        var page = session.Page;

        await page.Locator("#signin-user").FillAsync(userName);
        await page.Locator("#signin-password").FillAsync(ResetPassword);
        await page.Keyboard.PressAsync("Enter");

        // The notice comes before the page that was asked for.
        var notice = page.Locator("main [role=alert].reset-notice-text");
        await Assertions.Expect(page).ToHaveURLAsync(new Regex(@":\d+/$"));
        await Assertions.Expect(notice).ToContainTextAsync(text);
        await Assertions.Expect(notice).ToContainTextAsync(DateTime.Now.Year.ToString(System.Globalization.CultureInfo.InvariantCulture));
        await Assertions.Expect(notice).ToBeFocusedAsync();
        await AxeCheck.NoViolationsAsync(page);

        await page.GetByRole(AriaRole.Button, new() { Name = button, Exact = true }).ClickAsync();

        await Assertions.Expect(notice).ToHaveCountAsync(0);
        await Assertions.Expect(page.Locator("#main-content")).ToBeFocusedAsync();
        await page.ReloadAsync();
        await page.Locator("h1").WaitForAsync();
        await Assertions.Expect(page.Locator(".reset-notice")).ToHaveCountAsync(0);
    }

    // Browser mocks: "who am I" reports two resets that were not confirmed yet.
    [Theory]
    [InlineData(SupportedCultures.German, "2-mal", "3. November 2026", "Verstanden")]
    [InlineData(SupportedCultures.English, "2 times", "November 3, 2026", "Got it")]
    public async Task SeveralResets_AreAllListed_UntilConfirmed(string culture, string count, string laterDay, string button)
    {
        var fake = new FakeAccounts
        {
            AdminResets = [new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero)],
        };
        await using var session = await server.OpenAsync("/", culture, signedIn: true, async page =>
        {
            await fake.InstallAsync(page, signedIn: true);
            await fake.Mocks.RespondAsync(page, "POST", "/api/account/acknowledge-reset", 204);
        });
        var page = session.Page;

        var notice = page.Locator("main [role=alert].reset-notice-text");
        await Assertions.Expect(notice).ToContainTextAsync(count);
        await Assertions.Expect(notice.Locator("li")).ToHaveCountAsync(2);
        await Assertions.Expect(notice.Locator("li").Nth(1)).ToContainTextAsync(laterDay);
        await Assertions.Expect(notice).ToBeFocusedAsync();
        await AxeCheck.NoViolationsAsync(page);

        await page.GetByRole(AriaRole.Button, new() { Name = button, Exact = true }).ClickAsync();

        await Assertions.Expect(notice).ToHaveCountAsync(0);
        Assert.True(fake.Mocks.WasCalled("POST", "/api/account/acknowledge-reset"));
    }

    [Fact]
    public async Task WithoutAReset_ThereIsNoNotice()
    {
        await using var session = await server.OpenAsync("/", SupportedCultures.German);

        await Assertions.Expect(session.Page.Locator("main [role=alert]")).ToHaveCountAsync(0);
    }

    // Invites a member, then has the administrator create a reset link that the member uses.
    private async Task ResetByTheAdministratorAsync(string userName)
    {
        using var admin = Client();
        await SendAsync(admin, "api/auth/login", new SignInDto(ServerFixture.AdminUserName, ServerFixture.AdminPassword));
        var invitation = await SendAsync<LinkDto>(admin, "api/invitations");

        using var member = Client();
        await SendAsync(member, $"api/invitations/{TokenOf(invitation)}/accept", new AcceptInvitationDto(userName, "Zurückgesetzt", FirstPassword));
        var id = (await member.GetFromJsonAsync<AccountDto>("api/auth/me", Ct))!.Id;

        var reset = await SendAsync<LinkDto>(admin, $"api/members/{id}/reset-link");
        using var anonymous = Client();
        await SendAsync(anonymous, $"api/password-reset/{TokenOf(reset)}", new NewPasswordDto(ResetPassword));
    }

    private HttpClient Client()
    {
        var client = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer() }) { BaseAddress = server.BaseAddress };
        client.DefaultRequestHeaders.Add(ServerFixture.RequestHeader, "1");
        return client;
    }

    private static async Task SendAsync(HttpClient client, string path, object body)
    {
        using var response = await client.PostAsJsonAsync(path, body, Ct);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<T> SendAsync<T>(HttpClient client, string path)
    {
        using var response = await client.PostAsync(path, null, Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    private static string TokenOf(LinkDto link) => new Uri(link.Link).Segments[^1];
}
