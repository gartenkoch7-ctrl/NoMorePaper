namespace NoMorePaper.Application.Processing;

/// <summary>
/// Carries out one <see cref="JobStage"/> of a claimed job, registered keyed by that stage and resolved
/// from the job's scope by <see cref="ProcessNextDocument"/>. A runner records its own outcome (done,
/// failed, retried) through the queue.
/// </summary>
public interface IStageRunner
{
    /// <exception cref="OperationCanceledException">The Worker is stopping; the caller hands the job back.</exception>
    Task RunAsync(IJobQueue queue, JobClaim claim, CancellationToken stoppingToken);
}
