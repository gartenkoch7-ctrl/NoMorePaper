namespace NoMorePaper.Application.Recognition;

/// <summary>
/// Recognition failed: the LLM service is not reachable or refused, or its answer cannot be used (cut
/// off, not the expected JSON). Carries only a fixed technical reason such as an HTTP status code, never
/// the answer, which may quote the document; that makes it safe as a failure reason.
/// </summary>
public sealed class RecognitionException : Exception
{
    public RecognitionException()
    {
    }

    /// <param name="message">A fixed technical reason such as <c>answer cut off</c>; never model output.</param>
    public RecognitionException(string message)
        : base(message)
    {
    }

    /// <param name="message">A fixed technical reason; never model output.</param>
    /// <param name="innerException">The cause; its message is never used as a reason.</param>
    public RecognitionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
