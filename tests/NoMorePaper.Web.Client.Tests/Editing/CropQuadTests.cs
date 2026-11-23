using NoMorePaper.Contracts.Editing;
using NoMorePaper.Web.Client.Editing;
using Xunit;

namespace NoMorePaper.Web.Client.Tests.Editing;

public sealed class CropQuadTests
{
    [Fact]
    public void FromFractions_RoundsToTenthsOfAPercent_AndToFractionsGivesThemBack()
    {
        var quad = CropQuad.FromFractions([0.1234, 0.05, 0.9, 0.0504, 0.95, 0.9, 0.05, 0.95]);

        Assert.Equal(new CropPoint(123, 50), quad.TopLeft);
        Assert.Equal(new CropPoint(900, 50), quad.TopRight);
        Assert.Equal([0.123, 0.05, 0.9, 0.05, 0.95, 0.9, 0.05, 0.95], quad.ToFractions());
    }

    [Fact]
    public void FromFractions_PutsCornersOutsideThePageOnIt()
    {
        var quad = CropQuad.FromFractions([-0.2, -1e9, 1.3, 0, 1, 1, 0, 1]);

        Assert.Equal(new CropPoint(0, 0), quad.TopLeft);
        Assert.Equal(new CropPoint(1000, 0), quad.TopRight);
    }

    [Theory]
    [InlineData(new[] { 0.0, 0.0, 1.0 })]
    [InlineData(new[] { 0.0, double.NaN, 1, 0, 1, 1, 0, 1 })]
    public void FromFractions_NeedsFourFiniteCorners(double[] xy)
    {
        Assert.Throws<ArgumentException>(() => CropQuad.FromFractions(xy));
    }

    [Fact]
    public void IndexerAndWith_UseTheCornerNames()
    {
        var quad = CropQuad.Full.With(CropCorner.BottomRight, new CropPoint(800, 700));

        Assert.Equal(new CropPoint(800, 700), quad[CropCorner.BottomRight]);
        Assert.Equal(CropQuad.Full.TopLeft, quad[CropCorner.TopLeft]);
        Assert.Equal(new CropPoint(0, 1000), quad[CropCorner.BottomLeft]);
        Assert.Equal(new CropPoint(1000, 0), quad[CropCorner.TopRight]);
    }

    [Fact]
    public void ToSvgPoints_ListsTheCornersClockwise()
    {
        Assert.Equal("0,0 1000,0 1000,1000 0,1000", CropQuad.Full.ToSvgPoints());
    }

    [Fact]
    public void Clamped_MovesAPointOntoThePage()
    {
        Assert.Equal(new CropPoint(0, 1000), new CropPoint(-5, 1200).Clamped());
    }

    [Fact]
    public void Measure_OfAShape_UsesItsPageSize()
    {
        var shape = new CropShape(new CropQuad(new(0, 100), new(1000, 100), new(1000, 1000), new(0, 1000)), 595.28, 841.89);

        Assert.Equal(3, shape.Measure().TopCm);
    }

    [Fact]
    public void Measure_OnATurnedPage_NamesTheSidesAsSeen()
    {
        // 10 % cut off the stored top of an A4 page; turned right, that strip is on the right and 2.97 cm wide.
        var shape = new CropShape(new CropQuad(new(0, 100), new(1000, 100), new(1000, 1000), new(0, 1000)), 595.28, 841.89);

        Assert.Equal(new CropMeasure(0, 0, 3, 0, 0), shape.Measure(PageRotationDto.Right));
        Assert.Equal(new CropMeasure(0, 0, 0, 0, 3), shape.Measure(PageRotationDto.Left));
        Assert.Equal(new CropMeasure(0, 0, 0, 3, 0), shape.Measure(PageRotationDto.Half));
    }
}
