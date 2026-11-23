using Microsoft.Playwright;
using NoMorePaper.Contracts.Accounts;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Accounts;

/// <summary>The common frame of the pages for signed-out people: heading, logo, card, fields, layout.</summary>
public sealed class AccountFrameTests(ServerFixture server)
{
    private Task<BrowserSession> OpenAsync(string path, Func<IPage, Task>? prepare = null, bool setupNeeded = false)
    {
        var fake = new FakeAccounts { SetupNeeded = setupNeeded };
        return server.OpenAsync(path, SupportedCultures.German, signedIn: false, async page =>
        {
            await fake.InstallAsync(page);
            await fake.Mocks.RespondAsync(page, "GET", "/api/invitations/abc", 200, new InvitationDto(DateTimeOffset.UtcNow.AddDays(7)));
            if (prepare is not null)
            {
                await prepare(page);
            }
        });
    }

    [Theory]
    [InlineData("/anmelden", "Anmelden")]
    [InlineData("/einrichten", "Haushalt einrichten")]
    [InlineData("/einladung/abc", "Einladung annehmen")]
    [InlineData("/passwort/abc", "Neues Passwort setzen")]
    [InlineData("/notfall", "Passwort mit Notfall-Code ersetzen")]
    public async Task SignedOutPage_ShowsOneHeading_InTheFrame_WithADecorativeLogo(string path, string heading)
    {
        await using var session = await OpenAsync(path, setupNeeded: path == "/einrichten");
        var page = session.Page;
        await page.Locator("main [data-ready]").First.WaitForAsync();

        await Assertions.Expect(page.Locator("h1")).ToHaveCountAsync(1);
        await Assertions.Expect(page.Locator("h1")).ToHaveTextAsync(heading);
        await Assertions.Expect(page.Locator(".account-frame-card")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".account-frame-logo")).ToHaveAttributeAsync("aria-hidden", "true");
    }

    [Fact]
    public async Task SignIn_TabOrder_RunsFromTheFieldsToTheButtonAndTheEmergencyLink()
    {
        await using var session = await OpenAsync("/anmelden");
        var page = session.Page;
        await page.Locator("#signin-user").FocusAsync();

        await page.Keyboard.PressAsync("Tab");
        await Assertions.Expect(page.Locator("#signin-password")).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Tab");
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Passwort anzeigen: Passwort" })).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Tab");
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Anmelden" })).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Tab");
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Mit dem Notfall-Code ein neues Passwort setzen" })).ToBeFocusedAsync();
    }

    [Fact]
    public async Task FieldSymbols_AreHidden_AndTheFieldsKeepTheirLabels()
    {
        await using var session = await OpenAsync("/einrichten", setupNeeded: true);
        var page = session.Page;
        await page.Locator("#setup-user").WaitForAsync();

        var symbols = page.Locator(".form-field-icon");
        await Assertions.Expect(symbols).ToHaveCountAsync(4);
        for (var i = 0; i < 4; i++)
        {
            await Assertions.Expect(symbols.Nth(i)).ToHaveAttributeAsync("aria-hidden", "true");
        }

        await Assertions.Expect(page.GetByLabel("Benutzername")).ToHaveAttributeAsync("id", "setup-user");
    }

    [Fact]
    public async Task FocusedField_GetsAWhiteBorder()
    {
        await using var session = await OpenAsync("/anmelden");
        var field = session.Page.Locator("#signin-user");

        await field.FocusAsync();

        // Retrying: the border colour fades in over 150 ms.
        await Assertions.Expect(field).ToHaveCSSAsync("border-top-color", "rgb(255, 255, 255)");
    }

    [Fact]
    public async Task InvalidField_IsMarkedByADashedBorder_NotOnlyByColour()
    {
        await using var session = await OpenAsync("/anmelden");
        var page = session.Page;
        await page.Locator("#signin-user").FocusAsync();

        await page.Keyboard.PressAsync("Enter");

        var field = page.Locator("#signin-user");
        await Assertions.Expect(field).ToHaveAttributeAsync("aria-invalid", "true");
        var style = await field.EvaluateAsync<string>("el => getComputedStyle(el).borderTopStyle");
        Assert.Equal("dashed", style);
    }

    [Theory]
    [InlineData(390, 844)]
    [InlineData(320, 256)] // 1280 x 1024 at 400 % zoom
    public async Task SignIn_NeedsNoSideScrolling(int width, int height)
    {
        await using var session = await OpenAsync("/anmelden", page => page.SetViewportSizeAsync(width, height));
        var page = session.Page;
        await page.Locator("main [data-ready]").First.WaitForAsync();

        var fits = await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= window.innerWidth");
        Assert.True(fits);
    }

    [Theory]
    [InlineData(1280, 800)]
    [InlineData(390, 844)]
    [InlineData(320, 256)]
    public async Task EmergencyCode_IsFullyVisibleInItsField(int width, int height)
    {
        await using var session = await OpenAsync("/einrichten", async page =>
        {
            await page.SetViewportSizeAsync(width, height);
            await page.RouteAsync(new System.Text.RegularExpressions.Regex("/api/setup$"), route =>
                route.Request.Method == "POST"
                    ? route.FulfillAsync(new() { Status = 200, ContentType = "application/json", Body = "{\"emergencyCode\":\"ABCDEFGHIJKLMNOPQRST\"}" })
                    : route.FallbackAsync());
        }, setupNeeded: true);
        await SetupFlow.FillAndSubmitAsync(session.Page);
        var code = session.Page.Locator("#emergency-code");
        await code.WaitForAsync();

        var fits = await code.EvaluateAsync<bool>("el => el.scrollWidth <= el.clientWidth");
        Assert.True(fits);
    }

    [Fact]
    public async Task SignIn_LoadsNothingFromOtherHosts()
    {
        var hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var session = await OpenAsync("/anmelden", page =>
        {
            page.Request += (_, request) =>
            {
                if (Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) && uri.Scheme.StartsWith("http", StringComparison.Ordinal))
                {
                    lock (hosts)
                    {
                        hosts.Add(uri.Host);
                    }
                }
            };
            return Task.CompletedTask;
        });
        await session.Page.Locator("main [data-ready]").First.WaitForAsync();

        lock (hosts)
        {
            Assert.All(hosts, host => Assert.Equal(server.BaseAddress.Host, host));
        }
    }
}
