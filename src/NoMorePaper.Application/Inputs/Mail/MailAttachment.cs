namespace NoMorePaper.Application.Inputs.Mail;

/// <summary>A part of a mail besides its text (<see cref="MailContent.Attachments"/>).</summary>
/// <param name="FileName">Name the part came with; may be empty. Cleaned by the intake, never logged.</param>
/// <param name="DeclaredType">The MIME type the mail claims, e.g. <c>image/png</c>; only for the decision and the log, never trusted for the file.</param>
/// <param name="IsAttachment"><c>Content-Disposition: attachment</c>, as opposed to inline (logo, tracking pixel).</param>
/// <param name="Content">The decoded bytes.</param>
/// <param name="PixelWidth">Width of an image as its header says (JPEG, PNG), <c>null</c> when unknown.</param>
/// <param name="PixelHeight">Height of an image as its header says (JPEG, PNG), <c>null</c> when unknown.</param>
public sealed record MailAttachment(
    string FileName,
    string DeclaredType,
    bool IsAttachment,
    ReadOnlyMemory<byte> Content,
    int? PixelWidth,
    int? PixelHeight);
