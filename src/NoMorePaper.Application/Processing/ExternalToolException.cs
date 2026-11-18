namespace NoMorePaper.Application.Processing;

/// <summary>
/// A processing tool ended with an error. Carries only the tool and its exit code, never its
/// output, which may quote document content; that makes it safe as a failure reason.
/// </summary>
public sealed class ExternalToolException : Exception
{
    public ExternalToolException()
    {
    }

    public ExternalToolException(string message)
        : base(message)
    {
    }

    public ExternalToolException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <param name="tool">Program name, e.g. <c>ocrmypdf</c>; never a path or argument.</param>
    /// <param name="exitCode">Exit code the program ended with.</param>
    public ExternalToolException(string tool, int exitCode)
        : base($"{tool} exited with code {exitCode}.")
    {
        Tool = tool;
        ExitCode = exitCode;
    }

    /// <summary>Program name, e.g. <c>ocrmypdf</c>; empty when not set.</summary>
    public string Tool { get; } = string.Empty;

    public int ExitCode { get; }
}
