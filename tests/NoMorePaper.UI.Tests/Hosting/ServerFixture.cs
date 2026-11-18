using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NoMorePaper.Contracts.Accounts;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Testcontainers.PostgreSql;
using Xunit;

[assembly: AssemblyFixture(typeof(ServerFixture))]

namespace NoMorePaper.UI.Tests.Hosting;

/// <summary>
/// One Postgres container, one server process and one headless Chromium for the whole test run.
/// The server gets a real database connection and a Data Protection key folder, as in production.
/// </summary>
public sealed class ServerFixture : IAsyncLifetime
{
    private static readonly TimeSpan _serverStartTimeout = TimeSpan.FromMinutes(2);
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    // WASM start in a Debug build downloads many assemblies; give the first render enough time.
    private const float PageTimeoutMs = 60_000;

    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17").Build();
    private readonly string _dataProtectionPath =
        Path.Combine(Path.GetTempPath(), "nomorepaper-ui-tests", Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Optional: run the UI tests against an already running server (e.g. <c>docker compose up</c>)
    /// instead of starting Postgres and the server here.
    /// </summary>
    public const string ExternalBaseUrlVariable = "NOMOREPAPER_UI_TESTS_BASE_URL";

    private ServerProcess? _server;
    private Uri? _externalBaseAddress;
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    /// <summary>The administrator the fixture creates (or finds) when the server has accounts.</summary>
    public const string AdminUserName = "testadmin";

    /// <summary>Test-only password of <see cref="AdminUserName"/>; the server runs in a throw-away database.</summary>
    public const string AdminPassword = "Testpasswort-2026";

    /// <summary>False while the server has no accounts API; real-API tests skip then.</summary>
    public bool AccountsAvailable { get; private set; }

    public Uri BaseAddress => _externalBaseAddress ?? _server?.BaseAddress
        ?? throw new InvalidOperationException("Server not started.");

    public async ValueTask InitializeAsync()
    {
        var external = Environment.GetEnvironmentVariable(ExternalBaseUrlVariable);
        if (string.IsNullOrWhiteSpace(external))
        {
            await StartServerAsync();
        }
        else
        {
            _externalBaseAddress = new Uri(external);
        }

        await EnsureClientIsServedAsync();
        await SetUpHouseholdAsync();
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    private async Task StartServerAsync()
    {
        await _database.StartAsync();
        Directory.CreateDirectory(_dataProtectionPath);

        _server = ServerProcess.Start(new Dictionary<string, string>
        {
            ["ConnectionStrings__NoMorePaper"] = _database.GetConnectionString(),
            ["NoMorePaper__DataProtectionPath"] = _dataProtectionPath,
            // Without it the server falls back to /data, which is not writable on Linux CI runners.
            // A subfolder of the same temp folder, so the cleanup below removes it too.
            ["NoMorePaper__DataPath"] = Path.Combine(_dataProtectionPath, "data"),
        });
        await _server.WaitUntilReadyAsync(_serverStartTimeout);
    }

    // Fails the run in seconds with the server's answer, instead of every test waiting for a
    // WASM render that can never happen.
    private async Task EnsureClientIsServedAsync()
    {
        using var client = new HttpClient { BaseAddress = BaseAddress };
        using var response = await client.GetAsync(new Uri("_framework/blazor.webassembly.js", UriKind.Relative));
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Server does not serve the WASM client: {(int)response.StatusCode} for "
                + $"_framework/blazor.webassembly.js.{Environment.NewLine}{body[..Math.Min(body.Length, 1000)]}");
        }
    }

    // The accounts API only exists once track A is merged. Without it the fixture answers "who am I"
    // in the browser (see SignInContextAsync), so the page tests that do not need the real accounts
    // still run; the tests that do check AccountsAvailable and skip.
    private async Task SetUpHouseholdAsync()
    {
        using var client = new HttpClient { BaseAddress = BaseAddress };
        using var status = await client.GetAsync(new Uri("api/setup", UriKind.Relative));
        SetupStatusDto? setup = null;
        if (status.IsSuccessStatusCode && status.Content.Headers.ContentType?.MediaType == "application/json")
        {
            setup = await status.Content.ReadFromJsonAsync<SetupStatusDto>();
        }

        AccountsAvailable = setup is not null;
        if (setup is { Needed: true })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/setup")
            {
                Content = JsonContent.Create(new SetupRequestDto(AdminUserName, "Test Admin", AdminPassword)),
            };
            request.Headers.Add("X-NoMorePaper-Request", "1");
            using var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }
    }

    // Signs the browser context in: through the real API when there is one (the cookie lands in the
    // context), otherwise by answering the two calls the app makes at start.
    private async Task SignInContextAsync(IBrowserContext context)
    {
        if (AccountsAvailable)
        {
            var response = await context.APIRequest.PostAsync("api/auth/login", new APIRequestContextOptions
            {
                DataObject = new LoginRequestDto(AdminUserName, AdminPassword),
                Headers = new Dictionary<string, string> { ["X-NoMorePaper-Request"] = "1" },
            });
            if (!response.Ok)
            {
                throw new InvalidOperationException($"Test sign-in failed with HTTP {response.Status}.");
            }

            return;
        }

        var me = new MeDto(Guid.NewGuid(), AdminUserName, "Test Admin", true);
        await context.RouteAsync(new Regex("/api/auth/me$"), route => route.FulfillAsync(new RouteFulfillOptions
        {
            Status = 200,
            ContentType = "application/json",
            Body = JsonSerializer.Serialize(me, _json),
        }));
    }

    /// <summary>
    /// Opens <paramref name="path"/> in a fresh browser context and waits until the WASM app has
    /// rendered its H1. With <paramref name="culture"/> set, that language is preselected the way
    /// the language switch stores it; with null the app starts with nothing stored. The context is
    /// signed in as the test administrator unless <paramref name="signedIn"/> is false;
    /// <paramref name="prepare"/> runs on the page before it navigates (to mock API calls).
    /// </summary>
    public async Task<BrowserSession> OpenAsync(
        string path, string? culture = null, bool signedIn = true, Func<IPage, Task>? prepare = null)
    {
        var browser = _browser ?? throw new InvalidOperationException("Browser not started.");
        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = BaseAddress.ToString(),
            // A light OS preference proves the app stays dark regardless (no light mode by default).
            ColorScheme = ColorScheme.Light,
        });

        try
        {
            context.SetDefaultTimeout(PageTimeoutMs);
            if (culture is not null)
            {
                await context.AddInitScriptAsync(
                    $"localStorage.setItem('{LocalStorageCulturePreference.StorageKey}', '{culture}');");
            }

            if (signedIn)
            {
                await SignInContextAsync(context);
            }

            var page = await context.NewPageAsync();
            if (prepare is not null)
            {
                await prepare(page);
            }

            await page.GotoAsync(path);
            await page.Locator("h1").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
            return new BrowserSession(context, page);
        }
        catch
        {
            await context.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.DisposeAsync();
        }

        _playwright?.Dispose();

        if (_server is not null)
        {
            await _server.DisposeAsync();
        }

        await _database.DisposeAsync();

        if (Directory.Exists(_dataProtectionPath))
        {
            Directory.Delete(_dataProtectionPath, recursive: true);
        }
    }
}
