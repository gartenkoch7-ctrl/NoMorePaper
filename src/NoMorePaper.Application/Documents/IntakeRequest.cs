using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Documents;

/// <summary>A file one of the inputs (upload, camera, scanner folder, mail, iCloud) hands to <see cref="IntakeDocument"/>.</summary>
/// <param name="OwnerId">
/// The account the file belongs to; <c>null</c> when nobody can tell (e.g. a mail from an unknown sender):
/// then the administrator gets it, privately, marked for the owner check (<see cref="Document.NeedsOwnerCheck"/>).
/// </param>
/// <param name="FileName">Name the file came with; cleaned by the intake, so callers may pass it unchecked.</param>
/// <param name="MediaType">The detected type of the content (<see cref="FileSignature"/>), never a claimed one.</param>
/// <param name="Mail">"From the mail", for documents that came by mail.</param>
/// <param name="SplitFrom">The split document this file is a part of (<see cref="Document.MarkSplitPart"/>).</param>
public sealed record IntakeRequest(
    Guid? OwnerId,
    DocumentSource Source,
    string FileName,
    string MediaType,
    Stream Content,
    MailOrigin? Mail = null,
    Guid? SplitFrom = null);
