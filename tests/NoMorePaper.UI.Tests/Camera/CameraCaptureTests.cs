using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NoMorePaper.UI.Tests.Hosting;
using Xunit;

namespace NoMorePaper.UI.Tests.Camera;

/// <summary>
/// The whole camera flow with Chromium's fake camera playing <see cref="SheetVideo"/>: OpenCV.js finds
/// the sheet, the page hints "move right", takes the page by itself, and "Fertig" sends one JPEG part.
/// </summary>
public sealed partial class CameraCaptureTests(ServerFixture server, FakeCameraBrowser camera) : IClassFixture<FakeCameraBrowser>
{
    // Loading OpenCV.js (11 MB) in a Debug run and the first analyses take a while on a busy runner.
    private const float CameraTimeoutMs = 90_000;

    // Keeps every text the hint and the status showed, in order: they change faster than a test polls.
    private const string RecordTexts = """
        window.__camTexts = [];
        new MutationObserver(() => {
          for (const element of document.querySelectorAll('.camera-hint, .camera-status')) {
            const text = element.textContent.trim();
            if (text && window.__camTexts[window.__camTexts.length - 1] !== text) {
              window.__camTexts.push(text);
            }
          }
        }).observe(document, { subtree: true, childList: true, characterData: true });
        """;

    [Fact]
    public async Task FakeCamera_HintsRight_TakesThePageByItself_AndDoneSendsOneStraightenedJpeg()
    {
        var upload = new UploadRecorder();
        await using var session = await camera.OpenAsync(server, "/kamera", prepare: async page =>
        {
            await page.AddInitScriptAsync(RecordTexts);
            await page.RouteAsync(PagesUrl(), upload.FulfillAsync);
        });
        var page = session.Page;

        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Kamera starten" }).ClickAsync();

        var status = page.Locator("main .camera-status");
        await Assertions.Expect(status).ToContainTextAsync("Seite 1 aufgenommen", new() { Timeout = CameraTimeoutMs });
        var texts = await page.EvaluateAsync<string[]>("window.__camTexts");
        var hint = Array.IndexOf(texts, "Hinweis: Weiter nach rechts.");
        Assert.True(hint >= 0, "Texts shown: " + string.Join(" | ", texts));
        Assert.True(hint < Array.FindIndex(texts, text => text.StartsWith("Seite 1 aufgenommen", StringComparison.Ordinal)));

        // The same sheet stays in front of the camera: it is not taken twice.
        await page.WaitForTimeoutAsync(2500);
        await Assertions.Expect(page.Locator(".camera-pages li")).ToHaveTextAsync(["Seite 1"]);

        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Fertig" }).ClickAsync();

        await Assertions.Expect(status).ToContainTextAsync("Das Dokument wurde geteilt gespeichert");
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Neues Dokument in der Dokumentenliste zeigen" }))
            .ToHaveAttributeAsync("href", $"dokumente#doc-{UploadRecorder.Id:N}");
        await Assertions.Expect(page.Locator(".camera-no-pages")).ToBeVisibleAsync();

        var form = upload.Form ?? throw new InvalidOperationException("No upload was sent.");
        var jpeg = Assert.Single(form.Parts, part => part.Name == "page").Body;
        // The switch was not touched: the server applies the account's setting.
        Assert.Null(form.Value("private"));
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}[+-]\d{2}:\d{2}$", form.Value("takenAt"));

        // JFIF right after the start marker (PDFsharp needs it), and the sheet straightened: its edges
        // are 580–621 px long in the video, so the page is about 621 × 610 px.
        Assert.Equal([0xFF, 0xD8, 0xFF, 0xE0], jpeg[..4]);
        var (width, height) = JpegSize(jpeg);
        Assert.InRange(width, 590, 650);
        Assert.InRange(height, 580, 640);
    }

    [Fact]
    public async Task ManualShutter_WorksByKeyboard_AndDiscardTakesTheLastPageAway()
    {
        await using var session = await camera.OpenAsync(server, "/kamera");
        var page = session.Page;

        await page.Locator("#cam-start").FocusAsync();
        await page.Keyboard.PressAsync("Enter");

        // Focus moves to the shutter when the camera runs, so Enter takes a picture at once.
        await Assertions.Expect(page.Locator("#cam-capture")).ToBeFocusedAsync(new() { Timeout = CameraTimeoutMs });
        var status = page.Locator("main .camera-status");
        await Assertions.Expect(status).ToContainTextAsync("aufgenommen", new() { Timeout = CameraTimeoutMs });
        var before = await page.Locator(".camera-pages li").CountAsync();
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(page.Locator(".camera-pages li")).ToHaveCountAsync(before + 1);

        await page.Locator("#cam-discard").ClickAsync();

        await Assertions.Expect(page.Locator(".camera-pages li")).ToHaveCountAsync(before);
        await Assertions.Expect(status).ToContainTextAsync($"Letzte Seite verworfen. Seiten jetzt: {before}.");
    }

    private static (int Width, int Height) JpegSize(byte[] jpeg)
    {
        // Walk the segments to the frame header (SOF0–SOF2): height and width follow its precision byte.
        for (var at = 2; at + 9 < jpeg.Length;)
        {
            var marker = jpeg[at + 1];
            var length = (jpeg[at + 2] << 8) | jpeg[at + 3];
            if (marker is >= 0xC0 and <= 0xC2)
            {
                return ((jpeg[at + 7] << 8) | jpeg[at + 8], (jpeg[at + 5] << 8) | jpeg[at + 6]);
            }

            at += 2 + length;
        }

        throw new InvalidOperationException("No JPEG frame header found.");
    }

    [GeneratedRegex(@"/api/documents/pages$")]
    private static partial Regex PagesUrl();

    /// <summary>Answers the page upload with 202 and keeps the multipart body it received.</summary>
    private sealed class UploadRecorder
    {
        public static readonly Guid Id = Guid.Parse("0193a4b2-0000-7000-8000-000000000001");

        public MultipartForm? Form { get; private set; }

        public async Task FulfillAsync(IRoute route)
        {
            var request = route.Request;
            Form = MultipartForm.Parse(request.Headers["content-type"], request.PostDataBuffer ?? []);
            await route.FulfillAsync(new RouteFulfillOptions
            {
                Status = 202,
                ContentType = "application/json",
                Body = Encoding.UTF8.GetString(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new { id = Id })),
            });
        }
    }
}
