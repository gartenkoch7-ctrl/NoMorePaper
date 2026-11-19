using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MimeKit;
using NoMorePaper.Application.Inputs.Mail;

namespace NoMorePaper.Infrastructure.Inputs.Mail;

/// <summary>Turns a parsed MIME message into the <see cref="MailContent"/> of the mail input. Pure logic.</summary>
public static class MailMessageReader
{
    /// <param name="trustedAuthServId">The own mail server's <c>authserv-id</c> (<see cref="AuthenticationResults"/>).</param>
    /// <param name="fallbackDate">Used when the mail has no <c>Date</c> header (the time the server received it).</param>
    public static MailContent Read(MimeMessage message, string? trustedAuthServId, DateTimeOffset fallbackDate)
    {
        ArgumentNullException.ThrowIfNull(message);
        var from = message.From.Mailboxes.FirstOrDefault()?.Address?.Trim() ?? string.Empty;
        var check = AuthenticationResults.Evaluate(
            message.Headers.Where(header => header.Id == HeaderId.AuthenticationResults).Select(header => header.RawValue),
            MailText.Domain(from),
            trustedAuthServId);

        // HTML mails become plain text here; nothing is fetched from the net (ADR-0024).
        var text = message.TextBody ?? (message.HtmlBody is { } html ? HtmlText.Convert(html) : string.Empty);
        return new MailContent(
            from,
            check,
            message.Subject ?? string.Empty,
            message.Date == DateTimeOffset.MinValue ? fallbackDate : message.Date,
            text,
            Attachments(message));
    }

    /// <summary>
    /// What identifies a mail across folders and runs: its Message-ID with its date, as moving a mail keeps
    /// both; without a Message-ID a hash of date, sender, subject and size.
    /// </summary>
    public static string ItemKey(string? messageId, DateTimeOffset? date, string? from, string? subject, ulong? size)
    {
        var day = date?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? "-";
        if (!string.IsNullOrWhiteSpace(messageId))
        {
            return "id:" + messageId.Trim().Trim('<', '>') + "|" + day;
        }

        var fields = string.Join('\n', day, from ?? "-", subject ?? "-", size?.ToString(CultureInfo.InvariantCulture) ?? "-");
        return "hash:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(fields)));
    }

    private static List<MailAttachment> Attachments(MimeMessage message)
    {
        var parts = new List<MailAttachment>();
        foreach (var entity in message.BodyParts)
        {
            switch (entity)
            {
                // The text of the mail itself, as opposed to an attached text file.
                case TextPart text when !text.IsAttachment && (text.IsPlain || text.IsHtml):
                    continue;
                case MimePart part:
                    var content = Decode(part);
                    var size = ImageHeader.Size(content);
                    parts.Add(new MailAttachment(part.FileName ?? string.Empty, part.ContentType.MimeType, part.IsAttachment, content, size?.Width, size?.Height));
                    break;
                default:
                    // An attached mail (message/rfc822) is no document of its own; it is reported as left out.
                    parts.Add(new MailAttachment(string.Empty, entity.ContentType.MimeType, entity.IsAttachment, ReadOnlyMemory<byte>.Empty, null, null));
                    break;
            }
        }

        return parts;
    }

    private static byte[] Decode(MimePart part)
    {
        if (part.Content is null)
        {
            return [];
        }

        using var decoded = new MemoryStream();
        part.Content.DecodeTo(decoded);
        return decoded.ToArray();
    }
}
