namespace NoMorePaper.Application.Inputs.Mail;

/// <summary>How the connection to the IMAP server is encrypted (<see cref="MailboxSettings"/>).</summary>
public enum MailboxSecurity
{
    /// <summary>TLS from the start (usually port 993).</summary>
    Ssl,

    /// <summary>Plain connection upgraded with STARTTLS (usually port 143); refused if the server cannot.</summary>
    StartTls,

    /// <summary>No encryption; only for a server in the own network.</summary>
    None,
}
