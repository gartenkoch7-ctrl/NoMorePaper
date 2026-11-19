using System.Buffers.Binary;
using System.Text;
using MimeKit;

namespace NoMorePaper.Infrastructure.Tests.Inputs.Mail;

/// <summary>Synthetic mails for the IMAP tests; no real people or companies.</summary>
internal static class TestMessages
{
    public const string Sender = "rechnung@stadtwerke.example";

    public static MimeMessage Invoice(string subject, string from = Sender, bool pdf = true, string? authenticationResults = null)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(from));
        message.To.Add(MailboxAddress.Parse("dokumente@" + GreenMailFixture.Domain));
        message.Subject = subject;
        message.Date = new DateTimeOffset(2026, 11, 19, 8, 30, 0, TimeSpan.FromHours(1));
        message.MessageId = MimeKit.Utils.MimeUtils.GenerateMessageId("stadtwerke.example");
        var body = new BodyBuilder { TextBody = "Guten Tag,\nanbei Ihre Rechnung. Der Betrag wird am 30.11. abgebucht." };
        if (pdf)
        {
            // Unique bytes, so no two mails carry the same file.
            body.Attachments.Add("Rechnung.pdf", Encoding.ASCII.GetBytes("%PDF-1.4\n% " + Guid.NewGuid() + "\n%%EOF\n"), ContentType.Parse("application/pdf"));
        }

        message.Body = body.ToMessageBody();
        if (authenticationResults is not null)
        {
            // The own server puts its verdict on top of what came with the mail.
            message.Headers.Insert(0, new Header(HeaderId.AuthenticationResults, authenticationResults));
        }

        return message;
    }

    /// <summary>A mail with a photographed page attached: a JPEG whose header says 2480 × 3508 pixels, 40 KB.</summary>
    public static MimeMessage Photo(string subject)
    {
        var message = Invoice(subject, pdf: false);
        var body = new BodyBuilder { TextBody = "Foto vom Brief anbei." };
        body.Attachments.Add("Brief.jpg", Jpeg(2480, 3508, 40_000), ContentType.Parse("image/jpeg"));
        message.Body = body.ToMessageBody();
        return message;
    }

    public static MimeMessage Sample(string file) =>
        MimeMessage.Load(Path.Combine(AppContext.BaseDirectory, "Inputs", "Mail", "Samples", file));

    /// <summary>The start of a baseline JPEG (SOI, APP0, SOF0) padded to <paramref name="bytes"/>; enough for its header.</summary>
    public static byte[] Jpeg(int width, int height, int bytes)
    {
        var jpeg = new byte[bytes];
        byte[] head = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0, 1, 1, 0, 0, 1, 0, 1, 0, 0];
        head.CopyTo(jpeg, 0);
        jpeg[20] = 0xFF;
        jpeg[21] = 0xC0;
        BinaryPrimitives.WriteUInt16BigEndian(jpeg.AsSpan(22), 17);
        jpeg[24] = 8;
        BinaryPrimitives.WriteUInt16BigEndian(jpeg.AsSpan(25), (ushort)height);
        BinaryPrimitives.WriteUInt16BigEndian(jpeg.AsSpan(27), (ushort)width);
        return jpeg;
    }
}
