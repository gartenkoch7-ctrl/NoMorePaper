namespace NoMorePaper.Application.Inputs.Mail;

/// <summary>The stored mailbox as the settings page shows it: never the password, only whether there is one.</summary>
/// <param name="Settings">The set-up, or <c>null</c> when the mailbox was never set up.</param>
public sealed record MailboxSetup(MailboxSettings? Settings, bool HasPassword);
