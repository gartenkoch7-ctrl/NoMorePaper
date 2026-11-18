namespace NoMorePaper.Domain.Processing;

/// <summary>
/// When a failed processing attempt is tried again: after 1, 5 and 30 minutes, then never
/// (four attempts in total). The caller passes the time in, so the policy stays pure.
/// </summary>
public static class RetryPolicy
{
    /// <summary>Attempts in total, including the first one.</summary>
    public const int MaxAttempts = 4;

    private static readonly TimeSpan[] s_delays =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(30),
    ];

    /// <param name="attempt">Number of the attempt that just failed, starting at 1.</param>
    /// <param name="now">Time of the failure.</param>
    /// <returns>When to try again, or <c>null</c> to give up and mark the document as failed.</returns>
    public static DateTimeOffset? NextAttemptAt(int attempt, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(attempt, 1);
        return attempt <= s_delays.Length ? now + s_delays[attempt - 1] : null;
    }
}
