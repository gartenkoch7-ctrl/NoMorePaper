using Xunit;

namespace NoMorePaper.UI.Tests.Viewer;

/// <summary>
/// Expected colours of <see cref="ViewerPdfs.BlueBox"/> on screen or in a thumbnail, from the
/// table in docs/kb/darstellung.md ("Reinblau"): paper turns black, pure blue keeps its hue.
/// </summary>
internal static class BlueBoxColours
{
    private const int Tolerance = 10;

    /// <summary>Through the shader: blue #0000ff becomes #a3c1fe.</summary>
    public static void AssertShader(Rgb box, Rgb paper)
    {
        AssertClose("#000000", paper, "paper");
        AssertClose("#a3c1fe", box, "blue box");
    }

    /// <summary>Through the CSS fallback: blue #0000ff becomes #dadaff.</summary>
    public static void AssertFallback(Rgb box, Rgb paper)
    {
        AssertClose("#000000", paper, "paper");
        AssertClose("#dadaff", box, "blue box");
    }

    private static void AssertClose(string expected, Rgb actual, string what)
    {
        var distance = Rgb.Parse(expected).DistanceTo(actual);
        Assert.True(distance <= Tolerance, $"{what}: expected {expected} ± {Tolerance}, got {actual}");
    }
}
