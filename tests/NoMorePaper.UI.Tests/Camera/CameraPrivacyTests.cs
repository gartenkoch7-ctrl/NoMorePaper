using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NoMorePaper.UI.Tests.Hosting;
using Xunit;

namespace NoMorePaper.UI.Tests.Camera;

/// <summary>
/// The camera's "private" switch when the account's setting does not load, and a failed upload: the page
/// never claims "shared", sends no choice, and "Fertig" keeps the keyboard focus for the next try.
/// </summary>
public sealed partial class CameraPrivacyTests(ServerFixture server, FakeCameraBrowser camera) : IClassFixture<FakeCameraBrowser>
{
    private const float CameraTimeoutMs = 90_000;

    [Fact]
    public async Task WithoutTheAccountsSetting_TheSwitchSaysTheAccountDecides_NoChoiceIsSent_AndAFailedUploadKeepsFocusOnDone()
    {
        var uploads = new List<MultipartForm>();
        await using var session = await camera.OpenAsync(server, "/kamera", prepare: async page =>
        {
            await page.RouteAsync(PreferencesUrl(), route => route.Request.Method == "GET"
                ? route.FulfillAsync(new RouteFulfillOptions { Status = 500, ContentType = "application/problem+json", Body = "{}" })
                : route.FallbackAsync());
            await page.RouteAsync(PagesUrl(), route =>
            {
                uploads.Add(MultipartForm.Parse(route.Request.Headers["content-type"], route.Request.PostDataBuffer ?? []));
                return route.FulfillAsync(new RouteFulfillOptions { Status = 500, ContentType = "application/problem+json", Body = "{}" });
            });
        });
        var page = session.Page;

        await Assertions.Expect(page.Locator("#cam-private-hint")).ToContainTextAsync("Konto");
        Assert.True(await page.Locator("#cam-private").EvaluateAsync<bool>("box => box.indeterminate"));
        await Assertions.Expect(page.Locator("#cam-private")).Not.ToBeCheckedAsync();

        await page.Locator("#cam-start").ClickAsync();
        await Assertions.Expect(page.Locator("#cam-capture")).ToBeFocusedAsync(new() { Timeout = CameraTimeoutMs });
        await Assertions.Expect(page.Locator(".camera-pages li").First).ToBeVisibleAsync(new() { Timeout = CameraTimeoutMs });

        await page.Locator("#cam-done").FocusAsync();
        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(page.Locator("main [role=alert].form-error")).ToContainTextAsync("Das Dokument wurde nicht gespeichert", new() { Timeout = CameraTimeoutMs });
        await Assertions.Expect(page.Locator("#cam-done")).ToBeFocusedAsync();
        var form = Assert.Single(uploads);
        Assert.Null(form.Value("private"));
        Assert.Contains(form.Parts, part => part.Name == "page");
    }

    [GeneratedRegex(@"/api/account/preferences$")]
    private static partial Regex PreferencesUrl();

    [GeneratedRegex(@"/api/documents/pages$")]
    private static partial Regex PagesUrl();
}
