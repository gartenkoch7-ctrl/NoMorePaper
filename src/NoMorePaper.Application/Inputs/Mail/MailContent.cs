namespace NoMorePaper.Application.Inputs.Mail;

/// <summary>A mail as the mail input needs it (<see cref="IMailbox.ReadAsync"/>). Its content is never logged.</summary>
/// <param name="From">The address in the <c>From</c> header; empty when there is none.</param>
/// <param name="FromCheck">Whether the own mail server confirmed that address (<see cref="SenderCheck"/>).</param>
/// <param name="PlainText">The text part, or the HTML part turned into text; empty when there is neither.</param>
/// <param name="Attachments">Every part besides the text, inline images included; <see cref="MailIntake"/> picks what becomes a document.</param>
public sealed record MailContent(
    string From,
    SenderCheck FromCheck,
    string Subject,
    DateTimeOffset Date,
    string PlainText,
    IReadOnlyList<MailAttachment> Attachments);
