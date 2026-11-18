namespace NoMorePaper.Worker.Heartbeat;

/// <summary>Timing of the worker heartbeat.</summary>
internal static class HeartbeatSchedule
{
    /// <summary>One worker per installation for now; a fixed name keeps the table at one row.</summary>
    public const string WorkerName = "worker";

    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

    /// <summary>Several missed beats in a row before the worker counts as stalled.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(60);
}
