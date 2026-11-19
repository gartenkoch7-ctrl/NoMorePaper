using Microsoft.Playwright;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Camera;

/// <summary>
/// A headless Chromium of its own whose camera plays <see cref="SheetVideo"/>: the fake-device switches
/// apply to a whole browser, so the shared browser of <see cref="ServerFixture"/> keeps having no camera.
/// </summary>
public sealed class FakeCameraBrowser : IAsyncLifetime
{
    private const float PageTimeoutMs = 60_000;

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "nomorepaper-ui-camera", Guid.NewGuid().ToString("N"));
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public async ValueTask InitializeAsync()
    {
        var video = SheetVideo.Write(_folder);
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args =
            [
                "--use-fake-device-for-media-stream",
                "--use-fake-ui-for-media-stream",
                $"--use-file-for-fake-video-capture={video}",
                // Tones may play without a gesture, as after the start button on a phone.
                "--autoplay-policy=no-user-gesture-required",
            ],
        });
    }

    /// <summary>
    /// Opens <paramref name="path"/> signed in as the test administrator, with camera permission, in
    /// <paramref name="culture"/>; <paramref name="prepare"/> runs before the navigation (API mocks).
    /// </summary>
    public async Task<BrowserSession> OpenAsync(
        ServerFixture server, string path, string culture = SupportedCultures.German, Func<IPage, Task>? prepare = null)
    {
        ArgumentNullException.ThrowIfNull(server);
        var browser = _browser ?? throw new InvalidOperationException("Browser not started.");
        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = server.BaseAddress.ToString(),
            ColorScheme = ColorScheme.Light,
            Permissions = ["camera"],
        });
        try
        {
            context.SetDefaultTimeout(PageTimeoutMs);
            await context.AddInitScriptAsync(
                $"localStorage.setItem('{LocalStorageCulturePreference.StorageKey}', '{culture}');");
            await ServerFixture.SignInAsync(context, ServerFixture.AdminUserName, ServerFixture.AdminPassword);
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
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }
}
