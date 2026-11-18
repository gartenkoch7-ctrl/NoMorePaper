namespace NoMorePaper.Infrastructure.Processing;

/// <summary>
/// Queue entry for one document (table <c>processing_jobs</c>). Workers claim due jobs with
/// <c>FOR UPDATE SKIP LOCKED</c> and set <see cref="LockedUntil"/>, so a crashed Worker's job
/// becomes due again once the lock has expired.
/// </summary>
public sealed class ProcessingJob
{
    public required Guid DocumentId { get; init; }

    /// <summary>
    /// Attempts counted so far; 0 until a Worker claims the job. An attempt handed back on shutdown
    /// does not count.
    /// </summary>
    public int Attempt { get; set; }

    /// <summary>Token of the current claim, new for every claim; <c>null</c> while nobody works on it.</summary>
    public Guid? ClaimId { get; set; }

    /// <summary>Earliest time the job may run (again).</summary>
    public DateTimeOffset NextAttemptAt { get; set; }

    /// <summary>End of the current Worker's claim; <c>null</c> while nobody works on it.</summary>
    public DateTimeOffset? LockedUntil { get; set; }

    /// <summary>Technical reason of the last failed attempt, never document content.</summary>
    public string? LastError { get; set; }
}
