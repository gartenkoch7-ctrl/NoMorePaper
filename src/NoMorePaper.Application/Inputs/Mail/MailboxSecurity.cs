namespace NoMorePaper.Application.Inputs.Mail;

/// <summary>How the connection to the IMAP server is encrypted (<see cref="MailboxSettings"/>).</summary>
public enum MailboxSecurity
{
    /// <summary>TLS from the start (usually port 993).</summary>
    Ssl,

    /// <summary>Plain connection upgraded with STARTTLS (usually port 143); refused if the server cannot.</summary>
    StartTls,

    /// <summary>
    /// No encryption. Never accepted by the set-up (<see cref="ConfigureMailbox"/> answers
    /// <see cref="MailboxSetupStatus.Unencrypted"/>): password and mails would cross the network in clear text.
    /// It exists only for the test mail server of the integration tests, which speaks plain IMAP.
    /// </summary>
    None,
}
