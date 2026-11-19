using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Accounts;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Classification;

/// <summary>An editor confirms the fields as they are; the document leaves "to review" unless it is a possible duplicate.</summary>
public sealed class ConfirmDocument(IDocumentFieldsStore documents, ICurrentAccount current, ILogger<ConfirmDocument> logger)
{
    private const string Action = "confirm";

    /// <param name="seen">
    /// The recognition state the editor saw the fields with; another one now is <see cref="ChangeOutcome.Conflict"/>,
    /// so fields recognized, or confirmed for the first time, meanwhile are never confirmed unseen.
    /// </param>
    public async Task<ChangeOutcome> ConfirmAsync(Guid id, RecognitionState seen, CancellationToken cancellationToken)
    {
        var (document, outcome) = await DocumentChangeGuard.LoadAsync(
            documents, current, logger, id, Action, found => found.CanEdit(current.Id), cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return outcome;
        }

        // Compared under the row lock FindVisibleAsync took, so the Worker cannot finish in between.
        if (document.Recognition != seen)
        {
            logger.DocumentChangeRefused(current.Id, ChangeOutcome.Conflict, Action, id);
            return ChangeOutcome.Conflict;
        }

        document.Confirm(current.Id);
        await documents.SaveAsync(cancellationToken).ConfigureAwait(false);
        logger.FieldsConfirmed(id, current.Id);
        return ChangeOutcome.Changed;
    }
}
