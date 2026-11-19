namespace NoMorePaper.Application.Inputs.Mail;

/// <summary>Where a mail goes after its run (<see cref="IMailbox.MoveAsync"/>); the mailbox creates the folders.</summary>
public enum MailFolder
{
    /// <summary>"Verarbeitet": taken in, or judged to hold nothing worth keeping.</summary>
    Processed,

    /// <summary>"NoMorePaper-Spam": judged as spam; moved back into the inbox, it is taken without a second check.</summary>
    Spam,

    /// <summary>"NoMorePaper-Fehler": failed three times; reported on the page "Inputs".</summary>
    Failed,
}
