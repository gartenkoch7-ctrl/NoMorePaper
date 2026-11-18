namespace NoMorePaper.Application.Processing;

/// <summary>Result of <see cref="IJobQueue.FailAsync"/>.</summary>
/// <param name="Outcome">What was recorded.</param>
/// <param name="NextAttemptAt">When the job runs again; set only for <see cref="FailureOutcome.RetryScheduled"/>.</param>
public sealed record RecordedFailure(FailureOutcome Outcome, DateTimeOffset? NextAttemptAt = null);
