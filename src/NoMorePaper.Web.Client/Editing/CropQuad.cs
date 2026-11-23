using System.Globalization;

namespace NoMorePaper.Web.Client.Editing;

/// <summary>
/// The four corners of the part of a page that is kept, on the page as it is stored (not turned by the editor).
/// The corners may form any convex quadrilateral: the browser straightens it to a rectangle (perspective warp).
/// </summary>
public sealed record CropQuad(CropPoint TopLeft, CropPoint TopRight, CropPoint BottomRight, CropPoint BottomLeft)
{
    /// <summary>The whole page: where the corners start when no sheet was found.</summary>
    public static CropQuad Full { get; } = new(
        new CropPoint(0, 0), new CropPoint(CropPoint.Scale, 0), new CropPoint(CropPoint.Scale, CropPoint.Scale), new CropPoint(0, CropPoint.Scale));

    public CropPoint this[CropCorner corner] => corner switch
    {
        CropCorner.TopLeft => TopLeft,
        CropCorner.TopRight => TopRight,
        CropCorner.BottomRight => BottomRight,
        CropCorner.BottomLeft => BottomLeft,
        _ => throw new ArgumentOutOfRangeException(nameof(corner)),
    };

    public CropQuad With(CropCorner corner, CropPoint point) => corner switch
    {
        CropCorner.TopLeft => this with { TopLeft = point },
        CropCorner.TopRight => this with { TopRight = point },
        CropCorner.BottomRight => this with { BottomRight = point },
        CropCorner.BottomLeft => this with { BottomLeft = point },
        _ => throw new ArgumentOutOfRangeException(nameof(corner)),
    };

    /// <summary>
    /// From the corners as crop.js reports them: x and y of each corner as fractions 0–1, clockwise from the top
    /// left. Values outside the page are moved onto it.
    /// </summary>
    public static CropQuad FromFractions(IReadOnlyList<double> xy)
    {
        ArgumentNullException.ThrowIfNull(xy);
        if (xy.Count != 8 || xy.Any(value => !double.IsFinite(value)))
        {
            throw new ArgumentException("Four corners with x and y are needed.", nameof(xy));
        }

        CropPoint Point(int i) => new CropPoint(ToScale(xy[2 * i]), ToScale(xy[(2 * i) + 1])).Clamped();

        return new CropQuad(Point(0), Point(1), Point(2), Point(3));
    }

    /// <summary>The corners for crop.js: x and y of each as fractions 0–1, clockwise from the top left.</summary>
    public double[] ToFractions() =>
        [.. Corners.SelectMany(point => new[] { (double)point.X / CropPoint.Scale, (double)point.Y / CropPoint.Scale })];

    /// <summary>The corners as SVG polygon points in the 0–1000 view box of the frame.</summary>
    public string ToSvgPoints() =>
        string.Join(' ', Corners.Select(point => string.Create(CultureInfo.InvariantCulture, $"{point.X},{point.Y}")));

    private IEnumerable<CropPoint> Corners => [TopLeft, TopRight, BottomRight, BottomLeft];

    // Far outside values (a broken detection) must not overflow before they are clamped.
    private static int ToScale(double fraction) =>
        (int)Math.Round(Math.Clamp(fraction, -1, 2) * CropPoint.Scale, MidpointRounding.AwayFromZero);
}
