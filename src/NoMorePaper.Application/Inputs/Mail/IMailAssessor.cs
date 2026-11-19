namespace NoMorePaper.Application.Inputs.Mail;

/// <summary>
/// Asks the local LLM about a mail (ADR-0024): spam, whether a mail without attachment is a document worth
/// keeping, and a short note "from the mail". The text never leaves the own network (ADR-0002).
/// </summary>
public interface IMailAssessor
{
    /// <param name="knownSender">
    /// The sender belongs to an account (confirmed by the own mail server), or the mail came back from the spam
    /// folder: never spam then, only the document question and the note count.
    /// </param>
    /// <exception cref="LlmUnavailableException">The LLM did not answer; nothing is the mail's fault.</exception>
    /// <exception cref="InvalidDataException">The answer cannot be used.</exception>
    Task<MailAssessment> AssessAsync(MailText mail, bool knownSender, CancellationToken cancellationToken);
}
