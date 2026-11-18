namespace NoMorePaper.Application.Heartbeat;

/// <summary>Last signs of life of Worker processes; lets the Worker's health check see a stalled loop.</summary>
public interface IHeartbeatStore
{
    /// <summary>Records a heartbeat, replacing the worker's earlier one.</summary>
    Task RecordAsync(string workerName, DateTimeOffset beatAt, CancellationToken cancellationToken);

    /// <returns>Time of the worker's last heartbeat, or <c>null</c> if none is recorded.</returns>
    Task<DateTimeOffset?> GetLastBeatAsync(string workerName, CancellationToken cancellationToken);
}
