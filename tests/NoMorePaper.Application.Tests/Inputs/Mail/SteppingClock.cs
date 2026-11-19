namespace NoMorePaper.Application.Tests.Inputs.Mail;

/// <summary>A clock whose timestamp moves on by <see cref="Step"/> each time it is read.</summary>
internal sealed class SteppingClock : TimeProvider
{
    private long _ticks;

    public TimeSpan Step { get; set; }

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp()
    {
        _ticks += Step.Ticks;
        return _ticks;
    }

    public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 11, 19, 9, 0, 0, TimeSpan.Zero).AddTicks(_ticks);
}
