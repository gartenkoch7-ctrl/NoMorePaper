namespace NoMorePaper.Application.Processing;

/// <summary>
/// Which step a processing job is at. One job per document goes through the stages in order, each
/// with retries of its own (<see cref="Domain.Processing.RetryPolicy"/>).
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
}
