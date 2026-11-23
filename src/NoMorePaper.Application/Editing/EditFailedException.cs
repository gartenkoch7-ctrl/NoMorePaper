namespace NoMorePaper.Application.Editing;

/// <summary>
/// A planned edit cannot be carried out now (<see cref="IsPermanent"/>: never). Its message is a fixed
/// technical reason without document content, safe as the failure reason of the job.
/// </summary>
public sealed class EditFailedException : Exception
{
    public EditFailedException()
    {
    }

    public EditFailedException(string message)
        : base(message)
    {
    }

    public EditFailedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <param name="message">Fixed reason, e.g. "The page plan does not fit the document".</param>
    /// <param name="isPermanent">Another attempt would fail the same way.</param>
    public EditFailedException(string message, bool isPermanent)
        : base(message) => IsPermanent = isPermanent;

    /// <summary>Another attempt would fail the same way, so the edit is given up at once.</summary>
    public bool IsPermanent { get; }
}
