using Microsoft.Playwright;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Viewer;

/// <summary>
/// Pixel test of the "intelligent invert" shader against the values of docs/kb/darstellung.md:
/// white paper turns black, the red logo keeps its hue, a photo stays unchanged.
/// Runs on the GPU locally and on SwiftShader in CI.
/// </summary>
public sealed class ShaderPixelTests(ServerFixture server, ITestOutputHelper output)
{
    private const int Tolerance = 6;

    // Positions on the A4 test pages (spikes/viewer-shader/samples/src): the top margin, the left
    // part of the red logo circle (outside the yellow triangle) and the middle of the big photo.
    private static readonly (double X, double Y) _paper = (0.476, 0.034);
    private static readonly (double X, double Y) _logo = (30.0 / 210, 34.0 / 297);
    private static readonly (double X, double Y) _photo = (0.3, 0.40);

    [Fact]
    public async Task WhitePaperTurnsBlack_AndRedLogoKeepsItsHue()
    {
        await using var viewer = await OpenAsync("01-brief-logo.pdf");
        var page = viewer.Page;

        var inverted = await ScreenPixels.ReadAsync(page, _paper, _logo);
        await SwitchOriginalColorsAsync(page);
        var original = await ScreenPixels.ReadAsync(page, _paper, _logo);
        output.WriteLine($"{await RendererAsync(page)}: paper {original[0]} -> {inverted[0]}, logo {original[1]} -> {inverted[1]}");

        // The original confirms the positions; the inverted values are the documented ones.
        AssertClose("#ffffff", original[0], "paper, original");
        AssertClose("#c62828", original[1], "logo, original");
        AssertClose("#000000", inverted[0], "paper, inverted");
        AssertClose("#fe7368", inverted[1], "logo, inverted");
    }

    [Fact]
    public async Task PhotoStaysUnchanged()
    {
        await using var viewer = await OpenAsync("03-dokument-foto.pdf");
        var page = viewer.Page;

        var inverted = await ScreenPixels.ReadAsync(page, _photo, _paper);
        await SwitchOriginalColorsAsync(page);
        var original = await ScreenPixels.ReadAsync(page, _photo, _paper);
        output.WriteLine($"{await RendererAsync(page)}: photo {original[0]} -> {inverted[0]}, paper {original[1]} -> {inverted[1]}");

        AssertClose(original[0].ToString(), inverted[0], "photo");
        // Proves the shader ran: the paper next to the photo did turn black.
        AssertClose("#000000", inverted[1], "paper, inverted");
        Assert.True(original[0].DistanceTo(new Rgb(0, 0, 0)) > 20, $"Photo sample is black ({original[0]}): wrong position?");
    }

    private async Task<ViewerSession> OpenAsync(string sample)
    {
        // Tall enough that the whole stage, and with it the photo, is inside the screenshot.
        var viewer = await ViewerPages.OpenAsync(server, SupportedCultures.German, ViewerPdfs.Sample(sample),
            prepare: page => page.SetViewportSizeAsync(1280, 1600));
        await ViewerPages.WaitUntilShownAsync(viewer.Page);
        return viewer;
    }

    private static async Task SwitchOriginalColorsAsync(IPage page)
    {
        var toggle = page.GetByRole(AriaRole.Button, new() { Name = "Originalfarben" });
        await toggle.ClickAsync();
        await Assertions.Expect(toggle).ToHaveAttributeAsync("aria-pressed", "true");
        // The shader redraw is synchronous, but give the compositor a frame to show it.
        await page.EvaluateAsync("() => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)))");
    }

    // Which GPU path ran, for the log of the test (GPU locally, SwiftShader in CI).
    private static Task<string> RendererAsync(IPage page) => page.EvaluateAsync<string>("""
        () => {
          const gl = document.createElement('canvas').getContext('webgl2');
          if (!gl) return 'no WebGL2';
          const info = gl.getExtension('WEBGL_debug_renderer_info');
          return info ? gl.getParameter(info.UNMASKED_RENDERER_WEBGL) : gl.getParameter(gl.RENDERER);
        }
        """);

    private static void AssertClose(string expected, Rgb actual, string what)
    {
        var distance = Rgb.Parse(expected).DistanceTo(actual);
        Assert.True(distance <= Tolerance, $"{what}: expected {expected} ± {Tolerance}, got {actual}");
    }
}
