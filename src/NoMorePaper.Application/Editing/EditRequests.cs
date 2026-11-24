using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Documents;
using NoMorePaper.Application.Processing;
using NoMorePaper.Domain.Editing;

namespace NoMorePaper.Application.Editing;

/// <summary>
/// The steps every edit request shares: the page count a plan is checked against, the checks of the page
/// images, and queuing the edit in the change that locked the document.
/// </summary>
internal static class EditRequests
{
    // Pages of documents from before M4b are unknown until the backlog read them; the plan is then only
    // checked for its shape here, and the Worker checks it against the real count.
    private const int UnknownPageCount = 9999;

    public static async Task<int> PageCountAsync(IDocumentPages pages, Guid id, CancellationToken cancellationToken)
    {
        var known = (await pages.ListAsync(id, cancellationToken).ConfigureAwait(false)).Count;
        return known > 0 ? known : UnknownPageCount;
    }

    /// <returns>Why the images do not fit the replaced pages of <paramref name="steps"/> (a fixed word), or <c>null</c>.</returns>
    public static async Task<string?> CheckImagesAsync(
        IEnumerable<PageStep> steps, IReadOnlyList<EditPageImage> images, CancellationToken cancellationToken)
    {
        var replaced = steps.Where(step => step.Replaced).Select(step => step.SourcePage).ToHashSet();
        if (images.Count > EditPageImage.MaxImages)
        {
            return "too-many-images";
        }

        var given = images.Select(image => image.Page).ToList();
        if (given.Distinct().Count() != given.Count || !replaced.SetEquals(given))
        {
            return "images-do-not-match";
        }

        if (images.Any(image => image.Length is <= 0 or > EditPageImage.MaxBytes))
        {
            return "image-size";
        }

        // The type is read from the first bytes, never from what the browser claims.
        foreach (var image in images)
        {
            var head = new byte[FileSignature.HeadLength];
            var stream = image.Open();
            await using (stream.ConfigureAwait(false))
            {
                var read = await stream.ReadAtLeastAsync(head, head.Length, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
                if (FileSignature.DetectMediaType(head.AsSpan(0, read)) != "image/jpeg")
                {
                    return "not-jpeg";
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Queues the edit of a document loaded and locked through <see cref="Classification.IDocumentFieldsStore.FindVisibleAsync"/>,
    /// in the change it opened; a waiting page analysis gives way. Nothing is committed here, and a refusal
    /// leaves the change as it found it: a caller that commits it anyway changes nothing.
    /// </summary>
    /// <returns><c>false</c> while another edit or job of the document runs or waits; the caller then refuses without saving.</returns>
    public static async Task<bool> QueueAsync(
        IJobQueue queue, IDocumentEdits edits, ILogger logger, Guid id, EditKind kind, EditData data, Guid requestedBy, DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        // Read under the document's row lock, which every request and the Worker's commit take first: a
        // pending edit's plan must never be overwritten, not even in a change that is rolled back.
        if (await edits.GetAsync(id, cancellationToken).ConfigureAwait(false) is not null)
        {
            logger.EditRefusedBusy(kind, id);
            return false;
        }

        // Dropping succeeds only when the analysis was the document's only job, so queuing cannot fail after it.
        if (await queue.DropPendingAnalysisAsync(id, cancellationToken).ConfigureAwait(false))
        {
            logger.AnalysisReplaced(id);
        }

        await edits.SaveAsync(new DocumentEdit(id, kind, EditDataJson.ToJson(data), requestedBy, at, RecognizeAfter: false), cancellationToken)
            .ConfigureAwait(false);
        if (await queue.EnqueueEditAsync(id, recognizeAfter: false, cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        // The plan saved above was the only one (checked first): taking it back leaves nothing behind.
        await edits.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        logger.EditRefusedBusy(kind, id);
        return false;
    }

    /// <summary>Stores the images in the edit's own folder, so no other edit's images are touched.</summary>
    public static async Task SaveImagesAsync(
        IEditPageStorage storage, Guid id, Guid editId, IReadOnlyList<EditPageImage> images, CancellationToken cancellationToken)
    {
        foreach (var image in images)
        {
            var content = image.Open();
            await using (content.ConfigureAwait(false))
            {
                await storage.SaveAsync(id, editId, image.Page, content, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
