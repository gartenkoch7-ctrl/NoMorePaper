namespace NoMorePaper.Infrastructure.Tests.Processing;

/// <summary>A clock the test sets by hand.</summary>
internal sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}
