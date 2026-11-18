using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Accounts;

namespace NoMorePaper.Application.Documents;

/// <summary>
/// The owner makes a document private or shares it with the household again. For anybody else the
/// document does not exist, so a private document's existence is never revealed.
/// </summary>
public sealed class SetDocumentPrivacy(IDocumentRepository documents, ICurrentAccount current, ILogger<SetDocumentPrivacy> logger)
{
    /// <returns><c>false</c> if the document does not exist or belongs to another account.</returns>
    public async Task<bool> SetAsync(Guid id, bool isPrivate, CancellationToken cancellationToken)
    {
        var changed = await documents.SetPrivacyAsync(id, current.Id, isPrivate, cancellationToken).ConfigureAwait(false);
        if (changed)
        {
            logger.PrivacyChanged(id, isPrivate, current.Id);
        }

        return changed;
    }
}
