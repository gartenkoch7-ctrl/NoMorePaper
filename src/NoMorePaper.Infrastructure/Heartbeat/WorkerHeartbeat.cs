namespace NoMorePaper.Infrastructure.Heartbeat;

/// <summary>Last sign of life of a worker process; lets the Worker's health check see a stalled loop.</summary>
public sealed class WorkerHeartbeat
{
    public required string WorkerName { get; init; }

    public DateTimeOffset LastBeatAt { get; set; }
}
