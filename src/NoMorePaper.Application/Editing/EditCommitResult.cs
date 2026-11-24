namespace NoMorePaper.Application.Editing;

/// <summary>What <see cref="Processing.IJobQueue.CompleteEditAsync"/> did.</summary>
public enum EditCommitResult
{
    /// <summary>The new version is stored.</summary>
    Committed,

    /// <summary>Nothing was stored: the job is gone or another Worker has claimed it since.</summary>
    ClaimLost,

    /// <summary>Nothing was stored: the documents changed since they were read (<see cref="EditCompletion.Apply"/>).</summary>
    Changed,
}
