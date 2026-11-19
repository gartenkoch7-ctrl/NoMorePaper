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

    /// <summary>No password given and none stored.</summary>
    PasswordMissing,
}
