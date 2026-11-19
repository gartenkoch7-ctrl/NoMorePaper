namespace NoMorePaper.Application.Inputs.Mail;

/// <summary>
/// The local LLM did not answer the mail check: not reachable, refused, out of time, or busy with
/// recognition longer than a mail may wait. No fault of the mail: the run ends, the mail stays in the
/// inbox untouched and nothing is counted against it (<see cref="InputProblemKind.LlmUnavailable"/>).
/// </summary>
public sealed class LlmUnavailableException : Exception
{
    public LlmUnavailableException()
        : base("The local LLM is not available.")
    {
    }

    public LlmUnavailableException(string message)
        : base(message)
    {
    }

    public LlmUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
