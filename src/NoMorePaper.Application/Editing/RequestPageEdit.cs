using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Accounts;
using NoMorePaper.Application.Classification;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Domain.Editing;

namespace NoMorePaper.Application.Editing;

/// <summary>
/// "Speichern" in "Seiten bearbeiten" (ADR-0025): who may edit the fields may turn, delete, reorder and
/// replace pages of a processed document. The plan and the images of replaced pages are checked, the edit
/// is queued for the Worker under the row lock, and the images are stored in the edit's own folder only
/// once the queue accepted it, so a refused request never touches the images of a running edit.
/// </summary>
public sealed class RequestPageEdit(
    IDocumentFieldsStore documents,
    IDocumentPages pages,
    IDocumentEdits edits,
    Processing.IJobQueue queue,
    IEditPageStorage pageImages,
    ICurrentAccount current,
    TimeProvider time,
    ILogger<RequestPageEdit> logger)
{
    /// <param name="images">One JPEG per replaced page of <paramref name="plan"/>, none else.</param>
    /// <returns>
    /// <see cref="ChangeOutcome.Changed"/> when queued; <see cref="ChangeOutcome.Invalid"/> for a plan or images that do not
    /// fit; <see cref="ChangeOutcome.Conflict"/> while the document is not done or another job of it runs or waits.
    /// </returns>
    public Task<ChangeOutcome> RequestAsync(Guid id, PagePlan plan, IReadOnlyList<EditPageImage> images, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(images);
        return QueueAsync(
            id, EditKind.Pages, plan.Steps ?? [], plan.Validate, data => data with { Steps = plan.Steps }, images, cancellationToken);
    }

    /// <summary>The request of a page edit or a split, which differ only in their plan.</summary>
    internal async Task<ChangeOutcome> QueueAsync(
        Guid id,
        EditKind kind,
        IEnumerable<PageStep?> steps,
        Func<int, bool> fits,
        Func<EditData, EditData> withPlan,
        IReadOnlyList<EditPageImage> images,
        CancellationToken cancellationToken)
    {
        var action = kind == EditKind.Split ? "split" : "edit-pages";
        var (document, outcome) = await DocumentChangeGuard.LoadAsync(
            documents, current, logger, id, action, found => found.CanEdit(current.Id), cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return outcome;
        }

        if (document.State != ProcessingState.Done)
        {
            logger.DocumentChangeRefused(current.Id, ChangeOutcome.Conflict, action, id);
            return ChangeOutcome.Conflict;
        }

        var count = await EditRequests.PageCountAsync(pages, id, cancellationToken).ConfigureAwait(false);
        var reason = !fits(count) ? "plan-does-not-fit"
            : await EditRequests.CheckImagesAsync(steps.OfType<PageStep>(), images, cancellationToken).ConfigureAwait(false);
        if (reason is not null)
        {
            logger.EditInvalid(kind, id, reason);
            return ChangeOutcome.Invalid;
        }

        var now = time.GetUtcNow();
        var data = withPlan(new EditData(Guid.CreateVersion7(now)));
        if (!await EditRequests.QueueAsync(queue, edits, logger, id, kind, data, current.Id, now, cancellationToken).ConfigureAwait(false))
        {
            return ChangeOutcome.Conflict;
        }

        // A failed commit leaves the images behind rather than risk losing those of a committed edit; they
        // go with the document's files.
        await EditRequests.SaveImagesAsync(pageImages, id, data.EditId, images, cancellationToken).ConfigureAwait(false);
        await documents.SaveAsync(cancellationToken).ConfigureAwait(false);
        logger.EditRequested(current.Id, kind, id);
        return ChangeOutcome.Changed;
    }
}
