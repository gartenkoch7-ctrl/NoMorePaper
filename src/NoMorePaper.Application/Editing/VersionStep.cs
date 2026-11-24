using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Accounts;
using NoMorePaper.Application.Processing;
using NoMorePaper.Application.Trash;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Domain.Editing;

namespace NoMorePaper.Application.Editing;

/// <summary>
/// Makes a new version of a document (<see cref="EditKind.Pages"/>, <see cref="EditKind.Append"/>,
/// <see cref="EditKind.Rebuild"/>): composes the edited version with qpdf, runs OCR on what the document
/// keeps as its source, stages both files, stores the version with <see cref="VersionChanges"/> in one
/// transaction and then moves the files (<see cref="EditFinisher"/>). Until the commit the document
/// shows its previous version.
/// </summary>
internal sealed class VersionStep(
    EditSources sources,
    IOcrEngine ocr,
    IPageAnalyzer analyzer,
    IDocumentPages pages,
    IDocumentEdits edits,
    IAccountPreferencesStore preferences,
    EditFinisher finisher,
    TimeProvider time,
    ILogger<EditStageRunner> logger)
{
    private const string FallbackArchiveName = "archive.pdf";
    private const string EditedSuffix = "-edited.pdf";

    /// <param name="document">The job's document as read at the start of the attempt.</param>
    /// <returns><c>false</c> when the claim was lost and nothing was stored.</returns>
    /// <exception cref="EditFailedException">The edit cannot be carried out (now).</exception>
    public async Task<bool> RunAsync(
        IJobQueue queue, JobClaim claim, DocumentEdit edit, EditData data, Document document, string work, CancellationToken cancellationToken)
    {
        var started = time.GetTimestamp();
        var effective = document.VersionPolicy
            ?? (document.OwnerId is { } owner
                ? await preferences.GetAfterEditingAsync(owner, cancellationToken).ConfigureAwait(false)
                : VersionPolicy.KeepBoth);

        // A different policy is the document's own from now on (VersionRule.For), stored with the version; only an
        // owner's choice can be stored (Document.ChooseVersions).
        var policy = VersionRule.For(document, edit.Kind, effective);
        VersionPolicy? switchTo = policy == effective || document.OwnerId is null ? null : policy;

        Document? appended = null;
        (string Path, IReadOnlyList<bool?> Layers)? composed = null;
        if (edit.Kind == EditKind.Pages)
        {
            composed = await ComposePagesAsync(document, data, work, cancellationToken).ConfigureAwait(false);
        }
        else if (edit.Kind == EditKind.Append)
        {
            appended = await queue.ReadDocumentAsync(data.AppendedId!.Value, cancellationToken).ConfigureAwait(false) is { State: ProcessingState.Done } found
                ? found
                : throw new EditFailedException("The appended document is gone or not processed", isPermanent: true);

            // Read after the document: an edit of it stored since then still has files to move, which may not
            // be in place yet (its database row names the new ones); one stored later changes its hashes, which
            // the commit sees. Either way the pages are taken once it has ended.
            if (await edits.GetAsync(appended.Id, cancellationToken).ConfigureAwait(false) is not null)
            {
                throw new EditFailedException("The appended document is being edited", isPermanent: false);
            }
            composed = await ComposeAppendAsync(document, appended, work, cancellationToken).ConfigureAwait(false);
        }

        var keepsEdited = composed is not null && VersionRule.KeepsEdited(document, policy);
        var (input, mediaType, correctPages) = keepsEdited
            ? (composed!.Value.Path, EditSources.PdfMediaType, false)
            : await OcrSourceAsync(document, composed is null, work, cancellationToken).ConfigureAwait(false);

        var archivePath = Path.Combine(work, "archive.pdf");
        var result = await ocr.CreateArchiveAsync(input, mediaType, archivePath, correctPages, cancellationToken).ConfigureAwait(false);
        var analyzed = await analyzer.AnalyzeAsync(archivePath, input, cancellationToken).ConfigureAwait(false);
        var pageInfos = keepsEdited ? PageTextLayers.Apply(analyzed, composed!.Value.Layers) : analyzed;

        var editedFile = composed is { } made
            ? await sources.StageAsync(document.Id, data.EditId, DocumentFileKind.Edited, made.Path, EditedName(document), cancellationToken).ConfigureAwait(false)
            : null;
        var archiveName = document.FindFile(DocumentFileKind.Archive)?.FileName ?? FallbackArchiveName;
        var archiveFile = await sources.StageAsync(document.Id, data.EditId, DocumentFileKind.Archive, archivePath, archiveName, cancellationToken)
            .ConfigureAwait(false);

        var now = time.GetUtcNow();
        var changes = VersionChanges.For(document, policy, editedFile, archiveFile, now);
        List<TrashItem> items = [.. changes.Items];
        if (appended is not null)
        {
            items.Add(TrashItem.ForDocument(appended, now));
        }

        var finishing = data with { Finish = changes.Moves };
        var completion = new EditCompletion(
            result.Text, pageInfos, appended?.Id, items, EditDataJson.ToJson(finishing), Final: false,
            (tracked, other) => Apply(changes, appended, switchTo, tracked, other, now));
        switch (await queue.CompleteEditAsync(claim, completion, cancellationToken).ConfigureAwait(false))
        {
            case EditCommitResult.ClaimLost:
                return false;
            case EditCommitResult.Changed:
                throw new EditFailedException("The documents changed while the edit was made", isPermanent: false);
        }

        var elapsedMs = (long)time.GetElapsedTime(started).TotalMilliseconds;
        logger.EditStored(edit.Kind, document.Id, elapsedMs, result.PageCount);
        if (switchTo is { } switched)
        {
            logger.VersionPolicySwitched(edit.Kind, switched, document.Id);
        }

        return await finisher.FinishAsync(queue, claim, finishing, cancellationToken).ConfigureAwait(false);
    }

    private async Task<(string Path, IReadOnlyList<bool?> Layers)> ComposePagesAsync(
        Document document, EditData data, string work, CancellationToken cancellationToken)
    {
        var (source, count) = await sources.PagesSourceAsync(document, work, "source.pdf", archiveOnly: false, cancellationToken).ConfigureAwait(false);
        var plan = new PagePlan(data.Steps!);
        if (!plan.Validate(count))
        {
            throw new EditFailedException("The page plan does not fit the document", isPermanent: true);
        }

        var parts = await sources.PagesAsync(document.Id, data.EditId, source, plan.Steps, work, cancellationToken).ConfigureAwait(false);
        var output = Path.Combine(work, "edited.pdf");
        await sources.ComposeAsync(parts, output, cancellationToken).ConfigureAwait(false);
        var known = await pages.ListAsync(document.Id, cancellationToken).ConfigureAwait(false);
        return (output, PageTextLayers.Of(plan.Steps.Select(step => (step.SourcePage, step.Replaced)), known));
    }

    // The target's pages, then the appended document's, each unturned.
    private async Task<(string Path, IReadOnlyList<bool?> Layers)> ComposeAppendAsync(
        Document target, Document appended, string work, CancellationToken cancellationToken)
    {
        var (first, firstCount) = await sources.PagesSourceAsync(target, work, "source.pdf", archiveOnly: false, cancellationToken).ConfigureAwait(false);
        var (second, secondCount) = await sources.PagesSourceAsync(appended, work, "appended.pdf", archiveOnly: false, cancellationToken).ConfigureAwait(false);
        var output = Path.Combine(work, "edited.pdf");
        await sources.ComposeAsync([.. EditSources.AllPages(first, firstCount), .. EditSources.AllPages(second, secondCount)], output, cancellationToken)
            .ConfigureAwait(false);

        var targetPages = await pages.ListAsync(target.Id, cancellationToken).ConfigureAwait(false);
        var appendedPages = await pages.ListAsync(appended.Id, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<bool?> layers =
        [
            .. PageTextLayers.Of(Enumerable.Range(1, firstCount).Select(page => (page, false)), targetPages),
            .. PageTextLayers.Of(Enumerable.Range(1, secondCount).Select(page => (page, false)), appendedPages),
        ];
        return (output, layers);
    }

    // A rebuild runs OCR on the current source; "only original" on the original. A received original is
    // corrected as at intake, an edited version is not: its pages are as the user turned them.
    private async Task<(string Path, string MediaType, bool CorrectPages)> OcrSourceAsync(
        Document document, bool rebuild, string work, CancellationToken cancellationToken)
    {
        var source = (rebuild ? document.CurrentSource : document.FindFile(DocumentFileKind.Original))
            ?? throw new EditFailedException("The document has no source file", isPermanent: true);
        var path = await sources.CopyAsync(document.Id, source.Kind, work, "source-file", cancellationToken).ConfigureAwait(false);
        return (path, source.MediaType, source.Kind == DocumentFileKind.Original);
    }

    private static bool Apply(
        VersionChanges changes, Document? appended, VersionPolicy? switchTo, Document tracked, Document? other, DateTimeOffset now)
    {
        if (appended is not null
            && (other is not { State: ProcessingState.Done, OwnerId: { } } || !VersionRule.SameFiles(appended, other)))
        {
            return false;
        }

        if (!changes.ApplyTo(tracked))
        {
            return false;
        }

        // Stored as the owner's choice, like ChooseVersions does.
        if (switchTo is { } policy && tracked.OwnerId is { } policyOwner)
        {
            tracked.ChooseVersions(policyOwner, policy);
        }

        // A split suggestion named the pages as they were; the new version has others (or the user chose them).
        tracked.DismissSplit();

        if (other is { OwnerId: { } otherOwner })
        {
            // The result is private when either was (AppendRule); the request made it so, a share since is undone.
            if (AppendRule.ResultIsPrivate(tracked, other) && !tracked.IsPrivate && tracked.OwnerId is { } owner)
            {
                tracked.MakePrivate(owner);
            }

            // In the trash of its owner, whoever appended it (an editor may append, only the owner may trash).
            other.MoveToTrash(otherOwner, now);
        }

        return true;
    }

    // Offered on download next to the original: "Scan 1-edited.pdf".
    private static string EditedName(Document document)
    {
        var received = document.FindFile(DocumentFileKind.Original)?.FileName ?? document.FindFile(DocumentFileKind.Archive)?.FileName;
        var baseName = Path.GetFileNameWithoutExtension(received);
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = "document";
        }

        var maxBase = DocumentFile.MaxFileNameLength - EditedSuffix.Length;
        return (baseName.Length > maxBase ? baseName[..maxBase] : baseName) + EditedSuffix;
    }
}
