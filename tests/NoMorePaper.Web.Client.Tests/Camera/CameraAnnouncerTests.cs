using NoMorePaper.Web.Client.Camera;
using Xunit;

namespace NoMorePaper.Web.Client.Tests.Camera;

/// <summary>Camera hints are spoken at most every 2.5 s, the same one again only after 6 s.</summary>
public sealed class CameraAnnouncerTests
{
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 11, 19, 10, 0, 0, TimeSpan.Zero));
    private readonly CameraAnnouncer _announcer;

    public CameraAnnouncerTests() => _announcer = new CameraAnnouncer(_time);

    [Fact]
    public void TheFirstHint_IsSpokenAtOnce_AndOnlyOnce()
    {
        _announcer.Show("left");

        Assert.Equal("CamHintLeft", _announcer.Due());
        Assert.Null(_announcer.Due());
    }

    [Fact]
    public void ANewHint_WaitsForThePause_ThenIsSpokenWithoutAnotherChange()
    {
        _announcer.Show("left");
        _announcer.Due();

        _time.Advance(1000);
        _announcer.Show("closer");
        Assert.Null(_announcer.Due());

        _time.Advance(1400);
        Assert.Null(_announcer.Due());

        _time.Advance(100);
        Assert.Equal("CamHintCloser", _announcer.Due());
    }

    [Fact]
    public void TheSameHint_IsRepeatedOnlyAfterSixSeconds()
    {
        _announcer.Show("hold");
        _announcer.Due();

        _time.Advance(5900);
        Assert.Null(_announcer.Due());

        _time.Advance(100);
        Assert.Equal("CamHintHold", _announcer.Due());
    }

    [Theory]
    [InlineData("search", "CamHintSearch")]
    [InlineData("farther", "CamHintFarther")]
    [InlineData("closer", "CamHintCloser")]
    [InlineData("left", "CamHintLeft")]
    [InlineData("right", "CamHintRight")]
    [InlineData("up", "CamHintUp")]
    [InlineData("down", "CamHintDown")]
    [InlineData("hold", "CamHintHold")]
    public void EveryHintWord_HasItsText(string hint, string key)
    {
        _announcer.Show(hint);

        Assert.Equal(key, _announcer.Due());
        Assert.Equal(key, CameraAnnouncer.KeyFor(hint));
    }

    // "ready" is followed by the picture at once; unknown words come from a newer script.
    [Theory]
    [InlineData("ready")]
    [InlineData("sideways")]
    [InlineData(null)]
    public void HintsWithoutText_AreNotSpoken_AndDoNotStartThePause(string? hint)
    {
        _announcer.Show(hint);
        Assert.Null(_announcer.Due());

        _announcer.Show("up");
        Assert.Equal("CamHintUp", _announcer.Due());
    }

    [Fact]
    public void AnotherAnnouncement_StartsThePause_ThenTheSameHintMayFollowAtOnce()
    {
        _announcer.Show("right");
        _announcer.Due();
        _time.Advance(3000);

        // "Seite 1 aufgenommen" was just spoken.
        _announcer.Announced();
        Assert.Null(_announcer.Due());

        _time.Advance(2500);
        Assert.Equal("CamHintRight", _announcer.Due());
    }

    [Fact]
    public void AfterTheHintWasCleared_NothingIsSpoken()
    {
        _announcer.Show("right");
        _announcer.Show(null);

        Assert.Null(_announcer.Due());
    }
}
