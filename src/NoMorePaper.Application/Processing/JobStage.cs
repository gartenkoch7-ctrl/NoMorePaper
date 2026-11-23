namespace NoMorePaper.Application.Processing;

/// <summary>
/// Which step a processing job is at. One job per document goes through the stages in order, each
/// with retries of its own (<see cref="Domain.Processing.RetryPolicy"/>); a stage runner
/// (<see cref="IStageRunner"/>, keyed by stage) carries it out.
/// </summary>
public enum JobStage
{
    /// <summary>OCR and archive PDF; a failure for good marks the document as failed.</summary>
    Ocr,

    /// <summary>
    /// Fields by the local LLM, after OCR; a failure for good only removes the job, the document stays
    /// done without fields.
    /// </summary>
    Recognition,

    /// <summary>
    /// A planned edit of a done document (<see cref="Editing.DocumentEdit"/>): new edited version, OCR,
    /// new archive. Until it is complete the document keeps its previous version.
    /// </summary>
    Edit,

    /// <summary>Looks for documents in a multi-page scan (stack separation) with the local LLM, after OCR.</summary>
    Separation,
}
