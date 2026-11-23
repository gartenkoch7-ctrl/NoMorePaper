using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NoMorePaper.UI.Tests.Camera;
using NoMorePaper.UI.Tests.Hosting;
using Xunit;

namespace NoMorePaper.UI.Tests.Editing;

/// <summary>A cropped page goes with "Speichern" as JPEG part <c>page-&lt;n&gt;</c>, in an edit and in a split; memory stays free.</summary>
public sealed class PageCropSaveTests(ServerFixture server)
{
    private static readonly Regex Cropped = new("^Um 3 Grad gedreht", RegexOptions.None, TimeSpan.FromSeconds(1));

    [Fact]
    public async Task Save_SendsTheCroppedPageAsJfifJpeg_AndMarksItReplaced()
    {
        var fake = new FakePageEditor { Pdf = EditingPdfs.SkewedSheets };
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;
        await CropAutomaticallyAsync(page, 1);

        await page.GetByRole(AriaRole.Button, new() { Name = "Speichern" }).ClickAsync();

        await Assertions.Expect(page.Locator("#pe-saved-link")).ToBeVisibleAsync();
        var form = FormOf(fake, "edit");
        Assert.Equal(["plan", "page-1"], form.Parts.Select(part => part.Name));
        Assert.Contains("{\"sourcePage\":1,\"rotation\":\"None\",\"replaced\":true}", form.Value("plan"), StringComparison.Ordinal);
        Assert.Contains("{\"sourcePage\":2,\"rotation\":\"None\",\"replaced\":false}", form.Value("plan"), StringComparison.Ordinal);
        var jpeg = form.Parts[1].Body;
        // JFIF right after the start marker (the server's PDF writer needs it); the sheet straightened at 200 dpi
        // is 450 × 650 pt = 1250 × 1806 px.
        Assert.Equal([0xFF, 0xD8, 0xFF, 0xE0], jpeg[..4]);
        var (width, height) = JpegSize(jpeg);
        Assert.InRange(width, 1200, 1300);
        Assert.InRange(height, 1750, 1860);
    }

    [Fact]
    public async Task SaveWithASplitPoint_SendsTheSplitAsFormWithTheCroppedPage()
    {
        var fake = new FakePageEditor { Pdf = EditingPdfs.SkewedSheets };
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;
        await CropAutomaticallyAsync(page, 2);
        await page.Locator("#pe-split-1").ClickAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Speichern" }).ClickAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Zu „Zu prüfen“" })).ToBeVisibleAsync();
        var form = FormOf(fake, "split");
        Assert.Equal(["plan", "page-2"], form.Parts.Select(part => part.Name));
        Assert.Equal(
            "{\"parts\":[{\"steps\":[{\"sourcePage\":1,\"rotation\":\"None\",\"replaced\":false}]},"
            + "{\"steps\":[{\"sourcePage\":2,\"rotation\":\"None\",\"replaced\":true},{\"sourcePage\":3,\"rotation\":\"None\",\"replaced\":false}]}]}",
            form.Value("plan"));
        Assert.Equal([0xFF, 0xD8, 0xFF, 0xE0], form.Parts[1].Body[..4]);
    }

    [Fact]
    public async Task TwentyCrops_LeaveNoOpenCvMemoryBehind()
    {
        var fake = new FakePageEditor { Pdf = EditingPdfs.SkewedSheets };
        await using var session = await fake.OpenAsync(server);
        var page = session.Page;

        // OpenCV.js allocates in its WebAssembly heap, which never shrinks: a Mat left open per crop (a page at
        // 200 dpi is 15 MB) would grow it by hundreds of MB over 20 crops.
        var growth = await page.EvaluateAsync<double>(
            """
            async (url) => {
              const crop = await import('./js/editing/crop.js');
              const corners = [0.1, 0.1, 0.9, 0.12, 0.88, 0.9, 0.12, 0.88];
              const once = async () => {
                const found = await crop.detect(url, 1, null);
                if (found.error) throw new Error(found.error);
                await crop.apply(corners);
              };
              await once();
              const before = globalThis.cv.HEAPU8.length;
              for (let i = 0; i < 20; i++) await once();
              return globalThis.cv.HEAPU8.length - before;
            }
            """,
            $"api/documents/{fake.Id}/files/archive");

        Assert.InRange(growth, 0, 32 * 1024 * 1024);
    }

    private static async Task CropAutomaticallyAsync(IPage page, int number)
    {
        await page.Locator($"#pe-auto-{number}").ClickAsync();
        await Assertions.Expect(FakePageEditor.Status(page)).ToHaveTextAsync(Cropped, new() { Timeout = PageCropTests.CropTimeoutMs });
    }

    private static MultipartForm FormOf(FakePageEditor fake, string call)
    {
        Assert.True(fake.Sent.TryGetValue(call, out var sent), $"No {call} was sent.");
        Assert.StartsWith("multipart/form-data", sent.ContentType, StringComparison.Ordinal);
        return MultipartForm.Parse(sent.ContentType, sent.Body);
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
}
