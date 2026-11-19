using System.Globalization;
using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Documents;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Inputs.Mail;

/// <summary>
/// Turns a mail into documents (ADR-0024): each attachment that is a document becomes one, with "from the
/// mail"; a mail without such an attachment that is a document itself becomes a text PDF. Intake with
/// <see cref="DuplicateScope.Owned"/>, so a mail taken again after a failure adds no second copy of a file.
/// </summary>
public sealed class MailIntake(IntakeDocument intake, IPdfComposer pdf, ILogger<MailIntake> logger)
{
    /// <summary>Smaller images are logos, icons and tracking pixels.</summary>
    public const int MinImageBytes = 30 * 1024;

    /// <summary>Images with a shorter side are logos and banners, not photographed or scanned pages.</summary>
    public const int MinImagePixels = 500;

    private const string Pdf = "application/pdf";

    // Shown instead of a missing From header; MailOrigin needs an address.
    private const string UnknownSender = "unknown";

    /// <summary>
    /// Whether a part of a mail becomes a document, by its first bytes (<see cref="FileSignature"/>): a PDF
    /// always, an image only as a real attachment of at least <see cref="MinImageBytes"/> and
    /// <see cref="MinImagePixels"/> on each side (when its header says). Pure logic.
    /// </summary>
    /// <returns>The detected media type, or <c>null</c> to leave the part out.</returns>
    public static string? DocumentType(MailAttachment part)
    {
        ArgumentNullException.ThrowIfNull(part);
        var head = part.Content.Span[..Math.Min(part.Content.Length, FileSignature.HeadLength)];
        var detected = FileSignature.DetectMediaType(head);
        if (detected is null or Pdf)
        {
            return detected;
        }

        var bigEnough = part.Content.Length >= MinImageBytes
            && (part.PixelWidth ?? MinImagePixels) >= MinImagePixels
            && (part.PixelHeight ?? MinImagePixels) >= MinImagePixels;
        return part.IsAttachment && bigEnough ? detected : null;
    }

    /// <param name="ownerId">The account of the sender, or <c>null</c> for unknown (the administrator checks it).</param>
    /// <param name="keepText">Without document attachments: keep the mail text as a PDF.</param>
    /// <returns>How many new documents were added; files the owner had already count as taken, not as new.</returns>
    /// <exception cref="InvalidOperationException">Nobody can own the documents (household without administrator).</exception>
    public async Task<int> TakeAsync(MailContent mail, Guid? ownerId, string? note, bool keepText, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mail);
        var origin = new MailOrigin(mail.From.Length == 0 ? UnknownSender : mail.From, mail.Subject, note);
        var received = 0;
        var documents = 0;
        foreach (var part in mail.Attachments)
        {
            if (DocumentType(part) is not { } mediaType)
            {
                var declared = Word(part.DeclaredType);
                logger.PartSkipped(declared);
                continue;
            }

            documents++;
            using var content = new MemoryStream(part.Content.ToArray(), writable: false);
            received += await AcceptAsync(new IntakeRequest(ownerId, DocumentSource.Mail, part.FileName, mediaType, content, origin), cancellationToken)
                .ConfigureAwait(false);
        }

        if (documents > 0 || !keepText)
        {
            return received;
        }

        using var text = new MemoryStream();
        await pdf.ComposeTextAsync(TextPage(mail), text, cancellationToken).ConfigureAwait(false);
        text.Position = 0;
        var name = string.Create(CultureInfo.InvariantCulture, $"Mail {mail.Date:yyyy-MM-dd}.pdf");
        return await AcceptAsync(new IntakeRequest(ownerId, DocumentSource.Mail, name, Pdf, text, origin), cancellationToken).ConfigureAwait(false);
    }

    private async Task<int> AcceptAsync(IntakeRequest request, CancellationToken cancellationToken)
    {
        var result = await intake.AcceptAsync(request, null, DuplicateScope.Owned, cancellationToken).ConfigureAwait(false);
        if (result.Status == IntakeStatus.OwnerGone)
        {
            // The account was removed after its address was looked up: the sender is unknown now.
            request.Content.Position = 0;
            result = await intake.AcceptAsync(request with { OwnerId = null }, null, DuplicateScope.Owned, cancellationToken).ConfigureAwait(false);
        }

        return result.Status switch
        {
            IntakeStatus.Received => 1,
            IntakeStatus.AlreadyThere => 0,
            _ => throw new InvalidOperationException($"The mail cannot be taken in: {result.Status}."),
        };
    }

    // The labels are German like the household's documents; the PDF is the document, not the interface.
    private static TextPage TextPage(MailContent mail)
    {
        var subject = mail.Subject.Trim();
        var date = mail.Date.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
        return new TextPage(
            subject.Length == 0 ? "Mail" : subject,
            [new TextPageField("Von", mail.From.Length == 0 ? UnknownSender : mail.From), new TextPageField("Datum", date), new TextPageField("Betreff", subject)],
            mail.PlainText);
    }

    // Only the type and subtype as declared, never parameters such as a file name.
    private static string Word(string declaredType)
    {
        var type = declaredType.Split(';', 2)[0].Trim().ToLowerInvariant();
        return type.Length is > 0 and <= 100 && type.All(c => char.IsAsciiLetterOrDigit(c) || c is '/' or '.' or '+' or '-') ? type : "other";
    }
}
