using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Accounts;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Documents;

/// <summary>
/// Accepts a document from the browser (upload, camera): the signed-in account becomes the owner and
/// chooses per file whether it is private. An upload whose exact file the uploader may already see is
/// refused (M3). The work itself is <see cref="IntakeDocument"/>, shared with the background inputs.
/// </summary>
public sealed class ReceiveDocument(IntakeDocument intake, ICurrentAccount current, ILogger<ReceiveDocument> logger)
{
    /// <param name="fileName">Name the file was received with; cleaned by the intake, so callers may pass it unchecked.</param>
    /// <param name="mediaType">The detected type of the content, not what the uploader claimed.</param>
    /// <param name="isPrivate">
    /// Whether only the signed-in account, which becomes the owner, may see it; <c>null</c> when the browser sent
    /// no choice: the account's setting for new documents decides.
    /// </param>
    /// <param name="cancellationToken">Cancels storing the file; once the file is stored, the commit runs to its end.</param>
    /// <returns>
    /// The new, queued document; the document with the same file if the uploader may see one (nothing is
    /// kept); or that the signed-in account was removed (nothing is stored).
    /// </returns>
    public Task<ReceiveResult> ReceiveAsync(
        string fileName, string mediaType, Stream content, bool? isPrivate, CancellationToken cancellationToken) =>
        ReceiveAsync(fileName, mediaType, content, isPrivate, DocumentSource.Upload, cancellationToken);

    /// <inheritdoc cref="ReceiveAsync(string, string, Stream, bool?, CancellationToken)"/>
    /// <param name="source">How it came in: <see cref="DocumentSource.Upload"/> or <see cref="DocumentSource.Camera"/>.</param>
    public async Task<ReceiveResult> ReceiveAsync(
        string fileName, string mediaType, Stream content, bool? isPrivate, DocumentSource source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);

        // A member removed in the last minutes still has a valid cookie (ADR-0021); it must not add
        // documents. Removal right after this check is the same as removal right after the upload.
        if (!await current.ExistsAsync(cancellationToken).ConfigureAwait(false))
        {
            logger.AccountGone(current.Id, "upload");
            return ReceiveResult.AccountGone;
        }

        var request = new IntakeRequest(current.Id, source, fileName, mediaType, content);
        var accepted = await intake.AcceptAsync(request, isPrivate, DuplicateScope.Visible, cancellationToken).ConfigureAwait(false);
        return accepted switch
        {
            { Status: IntakeStatus.Received, DocumentId: { } id } => ReceiveResult.Received(id),
            { Status: IntakeStatus.AlreadyThere, DocumentId: { } existing } => ReceiveResult.Duplicate(existing),
            { Status: IntakeStatus.OwnerGone } => ReceiveResult.AccountGone,
            _ => throw new InvalidOperationException($"Unexpected intake result {accepted.Status} for a known owner."),
        };
    }
}
