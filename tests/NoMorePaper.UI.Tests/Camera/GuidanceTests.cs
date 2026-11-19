using Microsoft.Playwright;
using NoMorePaper.UI.Tests.Hosting;
using NoMorePaper.Web.Client.Localization;
using Xunit;

namespace NoMorePaper.UI.Tests.Camera;

/// <summary>
/// The pure camera rules in the browser that runs them: guidance.js as a table, the JPEG header rebuild
/// of capture.js, and edges.js with OpenCV.js on a drawn still image.
/// </summary>
public sealed class GuidanceTests(ServerFixture server)
{
    // A centred sheet covering 49 % of the frame, corners well inside.
    private const string Centred = "[{x:0.15,y:0.15},{x:0.85,y:0.15},{x:0.85,y:0.85},{x:0.15,y:0.85}]";

    public static TheoryData<string, string, string, bool> Cases() => new()
    {
        { "no sheet", "null", "[]", false },
        { "corner at the border", "[{x:0.01,y:0.1},{x:0.9,y:0.1},{x:0.9,y:0.9},{x:0.1,y:0.9}]", "[]", false },
        { "too small", "[{x:0.35,y:0.35},{x:0.65,y:0.35},{x:0.65,y:0.65},{x:0.35,y:0.65}]", "[]", false },
        { "left of centre", "[{x:0.05,y:0.2},{x:0.65,y:0.2},{x:0.65,y:0.8},{x:0.05,y:0.8}]", "[]", false },
        { "right of centre", "[{x:0.35,y:0.2},{x:0.95,y:0.2},{x:0.95,y:0.8},{x:0.35,y:0.8}]", "[]", false },
        { "above centre", "[{x:0.2,y:0.03},{x:0.8,y:0.03},{x:0.8,y:0.63},{x:0.2,y:0.63}]", "[]", false },
        { "below centre", "[{x:0.2,y:0.37},{x:0.8,y:0.37},{x:0.8,y:0.97},{x:0.2,y:0.97}]", "[]", false },
        { "centred, just found", Centred, "[]", false },
        { "centred, still for 0.8 s", Centred, $"[{{t:0,quad:{Centred}}},{{t:400,quad:{Centred}}}]", true },
        { "centred, still for 0.7 s only", Centred, $"[{{t:100,quad:{Centred}}},{{t:400,quad:{Centred}}}]", false },
        { "centred, lost once meanwhile", Centred, $"[{{t:0,quad:{Centred}}},{{t:400,quad:null}}]", false },
        { "centred, moved 2 % meanwhile", Centred, "[{t:0,quad:" + Centred + "},{t:400,quad:[{x:0.17,y:0.15},{x:0.87,y:0.15},{x:0.87,y:0.85},{x:0.17,y:0.85}]}]", false },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Assess_GivesTheHintAndReadiness_OfTheRules(string name, string quad, string history, bool ready)
    {
        await using var session = await server.OpenAsync("/", SupportedCultures.German);

        var result = await session.Page.EvaluateAsync<Assessment>(
            $"(async () => (await import('/js/camera/guidance.js')).assess({quad}, {history}, 800))()");

        var expected = name switch
        {
            "no sheet" => "search",
            "corner at the border" => "farther",
            "too small" => "closer",
            "left of centre" => "left",
            "right of centre" => "right",
            "above centre" => "up",
            "below centre" => "down",
            _ => ready ? "ready" : "hold",
        };
        Assert.Equal(expected, result.Hint);
        Assert.Equal(ready, result.Ready);
        Assert.InRange(result.Quality, name == "no sheet" ? 0 : 0.1, ready ? 1 : 0.95);
    }

    [Fact]
    public async Task Quality_RisesAsTheSheetFillsTheFrame_AndIsZeroWithoutOne()
    {
        await using var session = await server.OpenAsync("/", SupportedCultures.German);

        var qualities = await session.Page.EvaluateAsync<double[]>("""
            (async () => {
              const { assess } = await import('/js/camera/guidance.js');
              const square = (half) => [{x:0.5-half,y:0.5-half},{x:0.5+half,y:0.5-half},{x:0.5+half,y:0.5+half},{x:0.5-half,y:0.5+half}];
              return [assess(null, [], 0).quality, assess(square(0.1), [], 0).quality,
                      assess(square(0.2), [], 0).quality, assess(square(0.3), [], 0).quality];
            })()
            """);

        Assert.Equal(0, qualities[0]);
        Assert.True(qualities[1] < qualities[2] && qualities[2] < qualities[3], string.Join(", ", qualities));
    }

    [Fact]
    public async Task NewPage_NeedsTheSheetGoneForHalfASecond_OrMovedByATenth()
    {
        await using var session = await server.OpenAsync("/", SupportedCultures.German);

        var answers = await session.Page.EvaluateAsync<bool[]>($$"""
            (async () => {
              const { isNewPage } = await import('/js/camera/guidance.js');
              const moved = {{Centred}}.map((p) => ({ x: p.x + 0.11, y: p.y }));
              return [isNewPage({{Centred}}, {{Centred}}, 0), isNewPage({{Centred}}, null, 300),
                      isNewPage({{Centred}}, null, 500), isNewPage({{Centred}}, moved, 0)];
            })()
            """);

        Assert.Equal([false, false, true, true], answers);
    }

    [Fact]
    public async Task RebuildHeader_PutsJfifRightAfterTheStart_AndDropsExifAndComments()
    {
        await using var session = await server.OpenAsync("/", SupportedCultures.German);

        var bytes = await session.Page.EvaluateAsync<int[]>("""
            (async () => {
              const { rebuildHeader } = await import('/js/camera/capture.js');
              const exif = [0xFF,0xE1,0x00,0x08,0x45,0x78,0x69,0x66,0x00,0x00];
              const comment = [0xFF,0xFE,0x00,0x04,0x41,0x42];
              const table = [0xFF,0xDB,0x00,0x04,0x01,0x02];
              const scan = [0xFF,0xDA,0x00,0x02,0x11,0x22,0xFF,0xD9];
              return Array.from(rebuildHeader(new Uint8Array([0xFF,0xD8, ...exif, ...comment, ...table, ...scan])));
            })()
            """);

        int[] jfif = [0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00];
        Assert.Equal([0xFF, 0xD8, .. jfif, 0xFF, 0xDB, 0x00, 0x04, 0x01, 0x02, 0xFF, 0xDA, 0x00, 0x02, 0x11, 0x22, 0xFF, 0xD9], bytes);
    }

    [Fact]
    public async Task FindQuad_FindsTheCornersOfADrawnSheet()
    {
        await using var session = await server.OpenAsync("/", SupportedCultures.German);

        var corners = await session.Page.EvaluateAsync<double[]>("""
            (async () => {
              const { findQuad, loadOpenCv } = await import('/js/camera/edges.js');
              const cv = await loadOpenCv();
              const canvas = document.createElement('canvas');
              canvas.width = 640;
              canvas.height = 360;
              const g = canvas.getContext('2d');
              g.fillStyle = '#282828';
              g.fillRect(0, 0, 640, 360);
              g.fillStyle = '#ebebeb';
              g.beginPath();
              g.moveTo(250, 25); g.lineTo(560, 40); g.lineTo(550, 335); g.lineTo(260, 330);
              g.closePath();
              g.fill();
              return findQuad(cv, canvas).flatMap((p) => [p.x * 640, p.y * 360]);
            })()
            """);

        double[] expected = [250, 25, 560, 40, 550, 335, 260, 330];
        Assert.Equal(8, corners.Length);
        for (var i = 0; i < 8; i++)
        {
            Assert.InRange(corners[i], expected[i] - 6, expected[i] + 6);
        }
    }

    // Settable properties with a parameterless constructor: Playwright deserializes into it.
    public sealed record Assessment
    {
        public string Hint { get; init; } = "";

        public double Quality { get; init; }

        public bool Ready { get; init; }
    }
}
