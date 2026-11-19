namespace NoMorePaper.Application.Recognition;

/// <summary>What <see cref="Processing.IJobQueue.CompleteRecognitionAsync"/> did.</summary>
public enum RecognitionCommit
{
    /// <summary>The fields were taken and the job removed.</summary>
    Classified,

    /// <summary>
    /// Only the job was removed: there were no fields, or somebody confirmed the document during
    /// recognition, and confirmed fields are never overwritten.
    /// </summary>
    JobRemoved,

    /// <summary>Nothing was changed: the job is gone or another Worker has claimed it since.</summary>
    ClaimLost,
}
