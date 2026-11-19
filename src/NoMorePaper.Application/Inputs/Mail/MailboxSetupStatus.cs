namespace NoMorePaper.Application.Inputs.Mail;

/// <summary>How a call of <see cref="ConfigureMailbox"/> ended.</summary>
public enum MailboxSetupStatus
{
    /// <summary>Done (saved, or tested with the result alongside).</summary>
    Done,

    /// <summary>Only the administrator sets up the household's mailbox.</summary>
    Forbidden,

    /// <summary>Host, port, security, user name or authserv-id is not usable (<see cref="MailboxSettings.IsValid"/>).</summary>
    Invalid,

    /// <summary>The set-up asks for <see cref="MailboxSecurity.None"/>; only TLS or STARTTLS are accepted.</summary>
    Unencrypted,

    /// <summary>
    /// No password given, and none stored or the stored one is for another server or user (host, port, security
    /// or user name changed).
    /// </summary>
    PasswordMissing,
}
