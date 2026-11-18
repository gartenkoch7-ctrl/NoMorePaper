using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Accounts;

namespace NoMorePaper.Application.Documents;

/// <summary>
/// The owner makes a document private or shares it with the household again. For anybody else the
/// document does not exist, so a private document's existence is never revealed.
/// </summary>
public sealed class SetDocumentPrivacy(IDocumentRepository documents, ICurrentAccount current, ILogger<SetDocumentPrivacy> logger)
{
    public async Task<PrivacyChange> SetAsync(Guid id, bool isPrivate, CancellationToken cancellationToken)
    {
        if (!await current.ExistsAsync(cancellationToken).ConfigureAwait(false))
        {
            logger.AccountGone(current.Id, "set-privacy");
            return PrivacyChange.AccountGone;
        }

        if (!await documents.SetPrivacyAsync(id, current.Id, isPrivate, cancellationToken).ConfigureAwait(false))
        {
            return PrivacyChange.NotFound;
        }

        logger.PrivacyChanged(id, isPrivate, current.Id);
        return PrivacyChange.Changed;
    }
}
