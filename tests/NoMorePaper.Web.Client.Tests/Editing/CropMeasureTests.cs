using NoMorePaper.Web.Client.Editing;
using Xunit;

namespace NoMorePaper.Web.Client.Tests.Editing;

public sealed class CropMeasureTests
{
    // A4 in PDF points: 21 × 29.7 cm.
    private const double WidthPt = 595.28;
    private const double HeightPt = 841.89;

    [Fact]
    public void WholePage_TurnsNothing_AndCutsNothing()
    {
        Assert.Equal(new CropMeasure(0, 0, 0, 0, 0), CropMeasure.Of(CropQuad.Full, WidthPt, HeightPt));
    }

    [Fact]
    public void StraightFrame_CountsTheCentimetresOnEachSide()
    {
        // 10 % of 29.7 cm on top, 5 % of 21 cm on the right, 20 % below, nothing left.
        var quad = new CropQuad(new(0, 100), new(950, 100), new(950, 800), new(0, 800));

        Assert.Equal(new CropMeasure(0, 3, 1, 6, 0), CropMeasure.Of(quad, WidthPt, HeightPt));
    }

    [Fact]
    public void TiltedSheet_GivesItsAngle_MeasuredInPointsNotInShares()
    {
        // A sheet turned 3° clockwise around the page centre, 16 × 24 cm.
        var quad = Turned(3, 16, 24);

        var measure = CropMeasure.Of(quad, WidthPt, HeightPt);

        Assert.Equal(3, measure.Degrees);
        // The strip cut off along the whole side: the nearer corner counts.
        Assert.Equal(2, measure.TopCm);
        Assert.Equal(2, measure.LeftCm);
    }

    [Fact]
    public void TiltedTheOtherWay_GivesTheSameAngle()
    {
        Assert.Equal(4, CropMeasure.Of(Turned(-4, 16, 24), WidthPt, HeightPt).Degrees);
    }

    [Fact]
    public void Of_RejectsAPageWithoutSize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CropMeasure.Of(CropQuad.Full, 0, HeightPt));
    }

    private static CropQuad Turned(double degrees, double widthCm, double heightCm)
    {
        var angle = degrees * Math.PI / 180;
        var pageWidthCm = WidthPt / 72 * 2.54;
        var pageHeightCm = HeightPt / 72 * 2.54;
        CropPoint Corner(double dx, double dy)
        {
            // Screen coordinates (y down): clockwise is a positive angle.
            var x = (pageWidthCm / 2) + (dx * Math.Cos(angle)) - (dy * Math.Sin(angle));
            var y = (pageHeightCm / 2) + (dx * Math.Sin(angle)) + (dy * Math.Cos(angle));
            return new CropPoint((int)Math.Round(x / pageWidthCm * 1000), (int)Math.Round(y / pageHeightCm * 1000));
        }

        return new CropQuad(
            Corner(-widthCm / 2, -heightCm / 2), Corner(widthCm / 2, -heightCm / 2),
            Corner(widthCm / 2, heightCm / 2), Corner(-widthCm / 2, heightCm / 2));
    }
}
