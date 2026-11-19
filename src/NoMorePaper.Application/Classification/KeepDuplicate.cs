using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Accounts;

namespace NoMorePaper.Application.Classification;

/// <summary>An editor keeps a possible duplicate: the hint is removed, both documents stay.</summary>
public sealed class KeepDuplicate(IDocumentFieldsStore documents, ICurrentAccount current, ILogger<KeepDuplicate> logger)
{
    /// <returns><see cref="ChangeOutcome.Changed"/> also when there was no hint (nothing left to resolve).</returns>
    public async Task<ChangeOutcome> KeepAsync(Guid id, CancellationToken cancellationToken)
    {
        var (document, outcome) = await DocumentChangeGuard.LoadAsync(
            documents, current, logger, id, "keep-duplicate", found => found.CanEdit(current.Id), cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return outcome;
        }

        document.ClearPossibleDuplicate(current.Id);
        await documents.SaveAsync(cancellationToken).ConfigureAwait(false);
        logger.DuplicateKept(id, current.Id);
        return ChangeOutcome.Changed;
    }
}
