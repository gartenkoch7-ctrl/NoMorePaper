using NoMorePaper.Contracts.Editing;
using NoMorePaper.Web.Client.Editing;
using Xunit;

namespace NoMorePaper.Web.Client.Tests.Editing;

public sealed class CropGeometryTests
{
    private static readonly CropQuad Inset = new(new(100, 100), new(900, 100), new(900, 900), new(100, 900));

    [Theory]
    [InlineData(PageRotationDto.None, CropCorner.TopLeft, CropCorner.TopLeft)]
    [InlineData(PageRotationDto.Right, CropCorner.TopRight, CropCorner.TopLeft)]
    [InlineData(PageRotationDto.Right, CropCorner.TopLeft, CropCorner.BottomLeft)]
    [InlineData(PageRotationDto.Half, CropCorner.TopLeft, CropCorner.BottomRight)]
    [InlineData(PageRotationDto.Left, CropCorner.TopLeft, CropCorner.TopRight)]
    public void StoredCorner_IsTheCornerShownThereOnTheTurnedPage(PageRotationDto rotation, CropCorner visual, CropCorner stored)
        => Assert.Equal(stored, CropGeometry.StoredCorner(visual, rotation));

    [Theory]
    [InlineData(PageRotationDto.None, 200, 100)]
    [InlineData(PageRotationDto.Right, 900, 200)]
    [InlineData(PageRotationDto.Half, 800, 900)]
    [InlineData(PageRotationDto.Left, 100, 800)]
    public void ToVisual_TurnsClockwise_AndToStoredTurnsBack(PageRotationDto rotation, int x, int y)
    {
        var stored = new CropPoint(200, 100);

        var seen = CropGeometry.ToVisual(stored, rotation);

        Assert.Equal(new CropPoint(x, y), seen);
        Assert.Equal(stored, CropGeometry.ToStored(seen, rotation));
    }

    [Fact]
    public void VisualPoint_OfEveryCorner_MatchesTheStoredCornerAfterTheTurn()
    {
        var quad = new CropQuad(new(100, 50), new(950, 80), new(900, 980), new(20, 900));

        foreach (var rotation in Enum.GetValues<PageRotationDto>())
        {
            foreach (var corner in Enum.GetValues<CropCorner>())
            {
                var expected = CropGeometry.ToVisual(quad[CropGeometry.StoredCorner(corner, rotation)], rotation);
                Assert.Equal(expected, CropGeometry.VisualPoint(quad, rotation, corner));
            }
        }

        // Turned right, what is seen at the top left is the stored bottom left.
        Assert.Equal(new CropPoint(100, 20), CropGeometry.VisualPoint(quad, PageRotationDto.Right, CropCorner.TopLeft));
    }

    [Fact]
    public void MoveBy_RightOnAPageTurnedRight_MovesTheStoredCornerDown()
    {
        var moved = CropGeometry.MoveBy(Inset, PageRotationDto.Right, CropCorner.TopLeft, 10, 0);

        // The top left as seen is the stored bottom left; "right" as seen is "up" on the stored page.
        Assert.Equal(new CropPoint(100, 890), moved.BottomLeft);
        Assert.Equal(new CropPoint(110, 100), CropGeometry.VisualPoint(moved, PageRotationDto.Right, CropCorner.TopLeft));
        Assert.Equal(Inset.TopLeft, moved.TopLeft);
    }

    [Fact]
    public void MoveBy_FineSteps_StayExact()
    {
        var quad = CropQuad.Full;
        for (var i = 0; i < 15; i++)
        {
            quad = CropGeometry.MoveBy(quad, PageRotationDto.None, CropCorner.TopLeft, 2, 2);
        }

        Assert.Equal(new CropPoint(30, 30), quad.TopLeft);
    }

    [Fact]
    public void MoveTo_StaysOnThePage()
    {
        var moved = CropGeometry.MoveTo(Inset, PageRotationDto.None, CropCorner.TopLeft, new CropPoint(-300, -20));

        Assert.Equal(new CropPoint(0, 0), moved.TopLeft);
    }

    [Theory]
    [InlineData(PageRotationDto.None)]
    [InlineData(PageRotationDto.Right)]
    [InlineData(PageRotationDto.Half)]
    [InlineData(PageRotationDto.Left)]
    public void MoveTo_KeepsTheGapToTheNeighbours_SoTheFrameNeverFolds(PageRotationDto rotation)
    {
        var moved = CropGeometry.MoveTo(Inset, rotation, CropCorner.TopLeft, new CropPoint(1000, 1000));

        var topLeft = CropGeometry.VisualPoint(moved, rotation, CropCorner.TopLeft);
        Assert.Equal(new CropPoint(900 - CropGeometry.MinGap, 900 - CropGeometry.MinGap), topLeft);
    }

    [Fact]
    public void MoveTo_BottomRight_StaysBelowAndRightOfItsNeighbours()
    {
        var moved = CropGeometry.MoveTo(Inset, PageRotationDto.None, CropCorner.BottomRight, new CropPoint(0, 0));

        Assert.Equal(new CropPoint(100 + CropGeometry.MinGap, 100 + CropGeometry.MinGap), moved.BottomRight);
    }
}
