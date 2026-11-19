namespace NoMorePaper.Application.Inputs.Mail;

/// <summary>A mail cannot be read as MIME; this counts as a failure of the mail itself (<see cref="ProcessMailbox"/>).</summary>
public sealed class MailUnreadableException : Exception
{
    public MailUnreadableException()
        : base("The mail cannot be read.")
    {
    }

    public MailUnreadableException(string message)
        : base(message)
    {
    }

    public MailUnreadableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
