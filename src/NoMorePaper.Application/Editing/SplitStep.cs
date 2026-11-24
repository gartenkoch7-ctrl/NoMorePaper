using System.Globalization;
using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Classification;
using NoMorePaper.Application.Documents;
using NoMorePaper.Application.Processing;
using NoMorePaper.Application.Trash;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Domain.Editing;

namespace NoMorePaper.Application.Editing;

/// <summary>
/// Splits a document (<see cref="EditKind.Split"/>, ADR-0025): each part becomes a new document through the
/// intake, with owner, privacy, source and editors of the document, and is recognized anew like any new
/// document. Each part made is recorded in the plan at once, so a retried split goes on after it. Only
/// when all parts exist does the document go to the trash, in the transaction that ends the edit.
/// </summary>
internal sealed class SplitStep(
    EditSources sources,
    IntakeDocument intake,
    IDocumentFieldsStore fields,
    IDocumentEdits edits,
    EditFinisher finisher,
    TimeProvider time,
    ILogger<EditStageRunner> logger)
{
    /// <param name="document">The job's document as read at the start of the attempt.</param>
    /// <returns><c>false</c> when the claim was lost (e.g. the document went to the trash meanwhile).</returns>
    /// <exception cref="EditFailedException">The split cannot be carried out (now).</exception>
    public async Task<bool> RunAsync(
        IJobQueue queue, JobClaim claim, DocumentEdit edit, EditData data, Document document, string work, CancellationToken cancellationToken)
    {
        var started = time.GetTimestamp();
        var (source, count) = await sources.PagesSourceAsync(document, work, "archive-source.pdf", archiveOnly: true, cancellationToken).ConfigureAwait(false);
        var plan = new SplitPlan([.. data.Parts!.Select(steps => new PagePlan(steps))]);
        if (!plan.Validate(count))
        {
            throw new EditFailedException("The split plan does not fit the document", isPermanent: true);
        }

        List<Guid> created = [.. data.Created];
        for (var part = created.Count; part < plan.Parts.Count; part++)
        {
            var id = await CreatePartAsync(document, data.EditId, source, plan, part, work, cancellationToken).ConfigureAwait(false);
            created.Add(id);
            data = data with { Created = [.. created] };
            logger.SplitPartCreated(document.Id, part + 1, id);
            if (!await edits.UpdateDataAsync(document.Id, EditDataJson.ToJson(data), cancellationToken).ConfigureAwait(false))
            {
                return false;
            }
        }

        var now = time.GetUtcNow();
        var completion = new EditCompletion(
            null, null, null, [TrashItem.ForDocument(document, now)], EditDataJson.ToJson(data), Final: true,
            (tracked, _) =>
            {
                if (!VersionRule.SameFiles(document, tracked) || tracked.OwnerId is not { } owner)
                {
                    return false;
                }

                // In the trash of its owner, whoever split it (an editor may split, only the owner may trash). The
                // split answers a suggestion to split it; restored, the stack would offer it again.
                tracked.DismissSplit();
                tracked.MoveToTrash(owner, now);
                return true;
            });
        switch (await queue.CompleteEditAsync(claim, completion, cancellationToken).ConfigureAwait(false))
        {
            case EditCommitResult.ClaimLost:
                return false;
            case EditCommitResult.Changed:
                throw new EditFailedException("The document changed while it was split", isPermanent: false);
        }

        finisher.Discard(document.Id, data.EditId);
        var elapsedMs = (long)time.GetElapsedTime(started).TotalMilliseconds;
        logger.EditStored(edit.Kind, document.Id, elapsedMs, count);
        return true;
    }

    private async Task<Guid> CreatePartAsync(
        Document document, Guid editId, string source, SplitPlan plan, int part, string work, CancellationToken cancellationToken)
    {
        var pages = await sources.PagesAsync(document.Id, editId, source, plan.Parts[part].Steps, work, cancellationToken).ConfigureAwait(false);
        var output = Path.Combine(work, string.Create(CultureInfo.InvariantCulture, $"part-{part + 1}.pdf"));
        await sources.ComposeAsync(pages, output, cancellationToken).ConfigureAwait(false);

        // The title is only a start: recognition gives each part its own.
        var name = string.Create(CultureInfo.InvariantCulture, $"{document.Title} – {part + 1}.pdf");
        IntakeResult result;
        var content = sources.OpenRead(output);
        await using (content.ConfigureAwait(false))
        {
            var request = new IntakeRequest(
                document.OwnerId, document.Source, name, EditSources.PdfMediaType, content, document.MailOrigin, SplitFrom: document.Id);
            result = await intake.AcceptAsync(request, document.IsPrivate, DuplicateScope.Owned, cancellationToken).ConfigureAwait(false);
        }

        // A part with exactly the bytes of an own document is that document; the split goes on.
        if (result is not { Status: IntakeStatus.Received or IntakeStatus.AlreadyThere, DocumentId: { } id })
        {
            throw new EditFailedException($"The intake refused a part ({result.Status})", isPermanent: true);
        }

        // Also for a part found again: an attempt interrupted after storing it may not have taken them yet.
        if ((document.Editors.Count > 0 || document.NeedsOwnerCheck) && document.OwnerId is { } owner)
        {
            await InheritAsync(id, document, owner, cancellationToken).ConfigureAwait(false);
        }

        return id;
    }

    // The editors, and the owner check of a stack whose owner was unknown on arrival (ADR-0024): each part
    // may belong to somebody else, so the administrator must still be able to hand it over.
    private async Task InheritAsync(Guid id, Document document, Guid owner, CancellationToken cancellationToken)
    {
        // Only a part of this split: an own document that happens to have the same bytes keeps its own.
        var part = await fields.FindVisibleAsync(id, owner, cancellationToken).ConfigureAwait(false);
        if (part is not null && part.SplitFrom == document.Id)
        {
            if (document.Editors.Count > 0)
            {
                part.SetEditors(owner, document.Editors.Select(editor => editor.AccountId));
            }

            if (document.NeedsOwnerCheck && !part.NeedsOwnerCheck)
            {
                part.MarkOwnerCheck();
            }
        }

        await fields.SaveAsync(cancellationToken).ConfigureAwait(false);
    }
}
