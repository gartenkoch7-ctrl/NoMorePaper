namespace NoMorePaper.Web.Client.Tests.Camera;

/// <summary>A clock the test sets by hand; timers and timestamps stay on the system clock.</summary>
internal sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;

    // Whole milliseconds: fractional seconds as double would round below the thresholds under test.
    public void Advance(int milliseconds) => Now = Now.AddMilliseconds(milliseconds);
}
