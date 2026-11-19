namespace NoMorePaper.Application.Inputs.Mail;

/// <summary>A mail in the inbox (<see cref="IMailbox.NewMailAsync"/>).</summary>
/// <param name="Uid">The mail's unique id in the inbox (IMAP UID).</param>
/// <param name="ItemKey">
/// What identifies the mail for <see cref="ISeenItems"/> across folders and runs: its Message-ID, or, without
/// one, a hash of date, sender, subject and size. Known before the mail is read, so even a mail that cannot
/// be read counts its failures. Stored only as SHA-256; never logged.
/// </param>
public sealed record MailHandle(uint Uid, string ItemKey);
