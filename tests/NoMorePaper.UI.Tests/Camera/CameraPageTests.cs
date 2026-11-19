using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NoMorePaper.UI.Tests.Accounts;
using NoMorePaper.UI.Tests.Documents;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.UI.Tests.Keyboard;
using NoMorePaper.Web.Client.Camera;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Camera;

/// <summary>
/// The camera page with keyboard and screen reader: axe in both languages (before and while the camera
/// runs), the switches, and a clear message with a way to the upload when there is no camera.
/// </summary>
public sealed partial class CameraPageTests(ServerFixture server, FakeCameraBrowser camera) : IClassFixture<FakeCameraBrowser>
{
    private const float CameraTimeoutMs = 90_000;

    [Theory]
    [InlineData(SupportedCultures.German, "Kamera starten")]
    [InlineData(SupportedCultures.English, "Start camera")]
    public async Task BeforeTheStart_HasNoAxeViolations_AndLabelledSwitches(string culture, string start)
    {
        await using var session = await server.OpenAsync("/kamera", culture);
        var page = session.Page;

        await Assertions.Expect(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = start })).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#cam-tones")).ToHaveAttributeAsync("aria-describedby", "cam-tones-hint");
        await Assertions.Expect(page.Locator("#cam-private")).ToHaveAttributeAsync("aria-describedby", "cam-private-hint");
        await AxeCheck.NoViolationsAsync(page);
    }

    [Theory]
    [InlineData(SupportedCultures.German, "Auslösen")]
    [InlineData(SupportedCultures.English, "Take picture")]
    public async Task WhileTheCameraRuns_HasNoAxeViolations_AndTheVideoIsHiddenFromScreenReaders(string culture, string shutter)
    {
        await using var session = await camera.OpenAsync(server, "/kamera", culture);
        var page = session.Page;

        await page.Locator("#cam-start").ClickAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = shutter }))
            .ToBeFocusedAsync(new() { Timeout = CameraTimeoutMs });
        await Assertions.Expect(page.Locator("video")).ToHaveAttributeAsync("aria-hidden", "true");
        await AxeCheck.NoViolationsAsync(page);
    }

    [Fact]
    public async Task TabOrder_TonesPrivateStart_AllWithVisibleFocus()
    {
        await using var session = await server.OpenAsync("/kamera", SupportedCultures.German);
        var page = session.Page;

        var stops = new List<FocusState>();
        for (var i = 0; i < 30 && (stops.Count == 0 || stops[^1].Id != "cam-start"); i++)
        {
            await page.Keyboard.PressAsync("Tab");
            stops.Add(await FocusState.ReadAsync(page));
        }

        Assert.Equal(["cam-tones", "cam-private", "cam-start"], stops[^3..].Select(stop => stop.Id));
        Assert.All(stops[^3..], focus => Assert.True(focus.IndicatorWidth >= 3, $"Focus indicator {focus.IndicatorWidth}px on {focus.Id}"));
    }

    // The browser refuses the camera (an override of getUserMedia, as after "Don't allow"), or has no
    // camera API at all (plain http): the page explains and offers the upload.
    [Theory]
    [InlineData(SupportedCultures.German, "NotAllowedError", "Die Kamera ist nicht erlaubt.", "Stattdessen eine Datei hochladen")]
    [InlineData(SupportedCultures.English, "NotAllowedError", "Camera access is not allowed.", "Upload a file instead")]
    [InlineData(SupportedCultures.German, "NotFoundError", "Es wurde keine Kamera gefunden.", "Stattdessen eine Datei hochladen")]
    [InlineData(SupportedCultures.German, "NotReadableError", "von einer anderen App benutzt", "Stattdessen eine Datei hochladen")]
    [InlineData(SupportedCultures.German, null, "Dieser Browser erlaubt hier keine Kamera.", "Stattdessen eine Datei hochladen")]
    public async Task WithoutACamera_SaysWhyInText_AndLinksToTheUpload(string culture, string? refusal, string message, string link)
    {
        // Without a refusal the camera API is removed, as on plain http: what a real browser without a
        // camera reports differs between machines (Windows: no capture support, Linux CI: no device).
        await using var session = await server.OpenAsync("/kamera", culture, prepare: page => page.AddInitScriptAsync(refusal is null
            ? "Object.defineProperty(navigator, 'mediaDevices', { value: undefined, configurable: true });"
            : $"navigator.mediaDevices.getUserMedia = () => Promise.reject(new DOMException('refused', '{refusal}'));"));
        var page = session.Page;

        await page.Locator("#cam-start").ClickAsync();

        await Assertions.Expect(page.Locator("main [role=alert]")).ToContainTextAsync(message, new() { Timeout = CameraTimeoutMs });
        await Assertions.Expect(page.Locator(".camera-error")).ToContainTextAsync(message);
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = link })).ToHaveAttributeAsync("href", "hochladen");
        // The start button stays, so the person can try again after allowing the camera.
        await Assertions.Expect(page.Locator("#cam-start")).ToBeEnabledAsync();
        await AxeCheck.NoViolationsAsync(page);
    }

    [Fact]
    public async Task PrivateSwitch_IsPresetFromTheAccount()
    {
        await using var session = await ApiMocks.OpenViaNavigationAsync(server, SupportedCultures.German, "Kamera", page =>
            ApiMocks.RespondAsync(page, PreferencesUrl(), "GET", new { newDocumentsPrivate = true, mailSenders = Array.Empty<string>() }));
        var page = session.Page;

        await Assertions.Expect(page.Locator("h1")).ToHaveTextAsync("Mit der Kamera aufnehmen");
        await Assertions.Expect(page.Locator("#cam-private")).ToBeCheckedAsync();
    }

    [Fact]
    public async Task TonesSwitch_IsOnByDefault_AndRemembered()
    {
        await using var session = await server.OpenAsync("/kamera", SupportedCultures.German);
        var page = session.Page;
        await Assertions.Expect(page.Locator("#cam-tones")).ToBeCheckedAsync();

        await page.Locator("#cam-tones").UncheckAsync();
        await page.WaitForFunctionAsync($"localStorage.getItem('{CameraPreferences.TonesKey}') === 'false'");
        await page.ReloadAsync();

        await Assertions.Expect(page.Locator("#cam-private")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#cam-tones")).Not.ToBeCheckedAsync();
    }

    [GeneratedRegex(@"/api/account/preferences$")]
    private static partial Regex PreferencesUrl();
}
