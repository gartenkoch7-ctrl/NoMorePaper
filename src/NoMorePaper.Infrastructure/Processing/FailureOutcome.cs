namespace NoMorePaper.Infrastructure.Processing;

/// <summary>What <see cref="JobQueue.FailAsync"/> did with a failed attempt.</summary>
public enum FailureOutcome
{
    /// <summary>The job runs again at <see cref="RecordedFailure.NextAttemptAt"/>.</summary>
    RetryScheduled,

    /// <summary>The document is marked as failed and the job is removed.</summary>
    GivenUp,

    /// <summary>Nothing was changed: the job is gone or another Worker has claimed it since.</summary>
    ClaimLost,
}
