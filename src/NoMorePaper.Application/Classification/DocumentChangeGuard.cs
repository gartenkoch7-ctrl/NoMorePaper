using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Accounts;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Classification;

/// <summary>
/// The checks before a change to a document, in the order every such use case needs: the account still
/// exists, may see the document (else "not found", so a private document stays hidden), and may make this change.
/// </summary>
internal static class DocumentChangeGuard
{
    /// <param name="allowed">Whether the signed-in account may make the change, e.g. <see cref="Document.CanEdit"/>.</param>
    /// <param name="action">Fixed word for the log, e.g. <c>confirm</c>.</param>
    /// <returns>The tracked document and <see cref="ChangeOutcome.Changed"/>, or <c>null</c> and the refusal (logged).</returns>
    public static async Task<(Document? Document, ChangeOutcome Outcome)> LoadAsync(
        IDocumentFieldsStore documents,
        ICurrentAccount current,
        ILogger logger,
        Guid id,
        string action,
        Func<Document, bool> allowed,
        CancellationToken cancellationToken)
    {
        if (!await current.ExistsAsync(cancellationToken).ConfigureAwait(false))
        {
            logger.AccountGone(current.Id, action);
            return (null, ChangeOutcome.AccountGone);
        }

        var document = await documents.FindVisibleAsync(id, current.Id, cancellationToken).ConfigureAwait(false);
        var outcome = document is null ? ChangeOutcome.NotFound
            : !allowed(document) ? ChangeOutcome.Forbidden
            : ChangeOutcome.Changed;
        if (outcome == ChangeOutcome.Changed)
        {
            return (document, outcome);
        }

        logger.DocumentChangeRefused(current.Id, outcome, action, id);
        return (null, outcome);
    }
}
