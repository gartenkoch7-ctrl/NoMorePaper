namespace NoMorePaper.Web.Client.Editing;

/// <summary>
/// What a crop does to a page, in the words the editor says: how many degrees the sheet is turned straight and how
/// many centimetres are cut off on each side (whole numbers, as spoken). Pure logic.
/// </summary>
/// <param name="Degrees">How far the sheet was tilted, rounded; 0 when it was straight.</param>
public sealed record CropMeasure(int Degrees, int TopCm, int RightCm, int BottomCm, int LeftCm)
{
    private const double CmPerPoint = 2.54 / 72;

    /// <summary>Measures <paramref name="quad"/> on a page of <paramref name="widthPt"/> × <paramref name="heightPt"/> PDF points.</summary>
    /// <remarks>
    /// The tilt is the mean of the four edges against the page axes, measured in points (a fraction of the width
    /// is not as long as the same fraction of the height). A side counts what is cut off along its whole length:
    /// the corner nearer to the edge.
    /// </remarks>
    public static CropMeasure Of(CropQuad quad, double widthPt, double heightPt)
    {
        ArgumentNullException.ThrowIfNull(quad);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(widthPt);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(heightPt);
        double X(CropPoint point) => point.X * widthPt / CropPoint.Scale;
        double Y(CropPoint point) => point.Y * heightPt / CropPoint.Scale;

        // Each edge against its axis; a sheet turned clockwise gives a positive angle on all four.
        var top = Math.Atan2(Y(quad.TopRight) - Y(quad.TopLeft), X(quad.TopRight) - X(quad.TopLeft));
        var bottom = Math.Atan2(Y(quad.BottomRight) - Y(quad.BottomLeft), X(quad.BottomRight) - X(quad.BottomLeft));
        var left = Math.Atan2(X(quad.TopLeft) - X(quad.BottomLeft), Y(quad.BottomLeft) - Y(quad.TopLeft));
        var right = Math.Atan2(X(quad.TopRight) - X(quad.BottomRight), Y(quad.BottomRight) - Y(quad.TopRight));
        var degrees = Math.Abs((top + bottom + left + right) / 4 * 180 / Math.PI);

        return new CropMeasure(
            Whole(degrees),
            Cm(Math.Min(quad.TopLeft.Y, quad.TopRight.Y), heightPt),
            Cm(CropPoint.Scale - Math.Max(quad.TopRight.X, quad.BottomRight.X), widthPt),
            Cm(CropPoint.Scale - Math.Max(quad.BottomLeft.Y, quad.BottomRight.Y), heightPt),
            Cm(Math.Min(quad.TopLeft.X, quad.BottomLeft.X), widthPt));
    }

    private static int Cm(int share, double lengthPt) => Whole(Math.Max(0, share) * lengthPt / CropPoint.Scale * CmPerPoint);

    private static int Whole(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
}
