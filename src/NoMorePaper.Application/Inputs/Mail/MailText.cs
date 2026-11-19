using System.Text.RegularExpressions;
using NoMorePaper.Application.Recognition;

namespace NoMorePaper.Application.Inputs.Mail;

/// <summary>
/// What the local LLM gets of a mail (<see cref="IMailAssessor"/>): the sender's domain (never the whole
/// address), the subject, the shortened text and the types of the attachments that would become documents.
/// Pure logic.
/// </summary>
/// <param name="SenderDomain">The domain of the sender address, lower case; empty when there is none.</param>
/// <param name="AttachmentTypes">Detected media types of the attachments that would become documents.</param>
public sealed partial record MailText(string SenderDomain, string Subject, string Body, IReadOnlyList<string> AttachmentTypes)
{
    /// <summary>Longest text sent whole; a mail is shorter to judge than a document.</summary>
    public const int MaxLength = 3000;

    /// <summary>Characters kept from the start of a longer text (greeting, what it is about).</summary>
    public const int HeadLength = 2200;

    /// <summary>Characters kept from the end of a longer text (signature, imprint, unsubscribe link).</summary>
    public const int TailLength = 800;

    /// <summary>Longest subject sent.</summary>
    public const int MaxSubjectLength = 300;

    /// <param name="attachmentTypes">Detected media types of the attachments that become documents.</param>
    public static MailText From(MailContent mail, IReadOnlyList<string> attachmentTypes)
    {
        ArgumentNullException.ThrowIfNull(mail);
        ArgumentNullException.ThrowIfNull(attachmentTypes);
        var subject = mail.Subject.Trim();
        return new MailText(
            Domain(mail.From),
            subject.Length > MaxSubjectLength ? subject[..MaxSubjectLength] : subject,
            Shorten(mail.PlainText),
            attachmentTypes);
    }

    /// <summary>
    /// Links become <c>[Link: host]</c> (tracking addresses are long and say nothing, the host says much
    /// about phishing); then the padding is collapsed like OCR text, and a long text keeps head and tail.
    /// </summary>
    public static string Shorten(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var compact = RecognitionText.Compact(Links().Replace(text, "[Link: ${host}]"));
        if (compact.Length <= MaxLength)
        {
            return compact;
        }

        var headEnd = compact.LastIndexOf('\n', HeadLength - 1);
        headEnd = headEnd < HeadLength / 2 ? HeadLength : headEnd;
        var tailStart = compact.IndexOf('\n', compact.Length - TailLength);
        tailStart = tailStart < 0 || tailStart > compact.Length - (TailLength / 2) ? compact.Length - TailLength : tailStart + 1;

        // Never between the two halves of a character outside the BMP (emoji).
        headEnd = char.IsLowSurrogate(compact[headEnd]) ? headEnd - 1 : headEnd;
        tailStart = char.IsLowSurrogate(compact[tailStart]) ? tailStart + 1 : tailStart;
        return compact[..headEnd] + RecognitionText.Marker + compact[tailStart..];
    }

    /// <returns>The part after the last <c>@</c>, lower case; empty without one.</returns>
    public static string Domain(string address)
    {
        ArgumentNullException.ThrowIfNull(address);
        var at = address.LastIndexOf('@');
        return at < 0 ? string.Empty : address[(at + 1)..].Trim().TrimEnd('>').ToLowerInvariant();
    }

    [GeneratedRegex(@"https?://(?<host>[^/\s<>\]\)""'?#]+)[^\s<>\]\)""']*", RegexOptions.IgnoreCase)]
    private static partial Regex Links();
}
