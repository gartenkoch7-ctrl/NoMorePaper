namespace NoMorePaper.Application.Recognition;

/// <summary>
/// The local LLM stayed busy with other requests longer than the caller may wait (ADR-0024: recognition
/// and the mail check share one Ollama, one request at a time). Nothing was asked; this is no failure of
/// the request itself, so callers hand their work back instead of counting an attempt.
/// </summary>
public sealed class LlmBusyException : Exception
{
    public LlmBusyException()
        : base("The local LLM is busy.")
    {
    }

    public LlmBusyException(string message)
        : base(message)
    {
    }

    public LlmBusyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
