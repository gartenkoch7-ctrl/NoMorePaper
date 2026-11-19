using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Accounts;

namespace NoMorePaper.Application.Classification;

/// <summary>An editor confirms the fields as they are; the document leaves "to review" unless it is a possible duplicate.</summary>
public sealed class ConfirmDocument(IDocumentFieldsStore documents, ICurrentAccount current, ILogger<ConfirmDocument> logger)
{
    public async Task<ChangeOutcome> ConfirmAsync(Guid id, CancellationToken cancellationToken)
    {
        var (document, outcome) = await DocumentChangeGuard.LoadAsync(
            documents, current, logger, id, "confirm", found => found.CanEdit(current.Id), cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return outcome;
        }

        document.Confirm(current.Id);
        await documents.SaveAsync(cancellationToken).ConfigureAwait(false);
        logger.FieldsConfirmed(id, current.Id);
        return ChangeOutcome.Changed;
    }
}
