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

    /// <summary>
    /// Nothing was taken: the document was shared since the fields were made from the owner's private
    /// view. The job is handed back (the attempt does not count) to run again with the household's view.
    /// </summary>
    SharedMeanwhile,
}
