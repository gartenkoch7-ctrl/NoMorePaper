using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Accounts;
using NoMorePaper.Domain.Classification;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Classification;

/// <summary>
/// An editor (owner or chosen member) sets the fields of a document; they count as confirmed. Entries
/// are chosen by id and must be visible to the editor, so nobody can attach (and thereby learn of)
/// another account's private entry.
/// </summary>
public sealed class EditDocumentFields(IDocumentFieldsStore documents, ICurrentAccount current, ILogger<EditDocumentFields> logger)
{
    private const string Action = "edit-fields";

    public async Task<ChangeOutcome> EditAsync(Guid id, FieldsEdit edit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(edit);

        // Checked before the document is looked up: the answer must not depend on whether it exists.
        if (ToFields(edit) is not { } fields)
        {
            return ChangeOutcome.Invalid;
        }

        var (document, outcome) = await DocumentChangeGuard.LoadAsync(
            documents, current, logger, id, Action, found => found.CanEdit(current.Id), cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return outcome;
        }

        // Under the row lock FindVisibleAsync took: fields recognized, or confirmed for the first time,
        // since the editor loaded them are never overwritten unseen (later edits of confirmed fields are
        // not detected: the state stays confirmed).
        if (document.Recognition != edit.SeenRecognition)
        {
            logger.DocumentChangeRefused(current.Id, ChangeOutcome.Conflict, Action, id);
            return ChangeOutcome.Conflict;
        }

        if (!await EntriesVisibleAsync(fields, cancellationToken).ConfigureAwait(false))
        {
            logger.DocumentChangeRefused(current.Id, ChangeOutcome.Invalid, Action, id);
            return ChangeOutcome.Invalid;
        }

        document.Edit(current.Id, fields);
        await documents.SaveAsync(cancellationToken).ConfigureAwait(false);
        logger.FieldsEdited(id, current.Id);
        return ChangeOutcome.Changed;
    }

    private static DocumentFields? ToFields(FieldsEdit edit)
    {
        if (edit.TagIds is null || (edit.Amount is null && edit.Currency is not null))
        {
            return null;
        }

        try
        {
            var amount = edit.Amount is { } value ? new Money(value, edit.Currency ?? string.Empty) : null;
            var summary = string.IsNullOrWhiteSpace(edit.Summary) ? null : edit.Summary;
            return new DocumentFields(
                edit.Title, edit.CorrespondentId, edit.DocumentTypeId, edit.DocumentDate, amount, summary, edit.TagIds);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private async Task<bool> EntriesVisibleAsync(DocumentFields fields, CancellationToken cancellationToken) =>
        await VisibleAsync(ClassificationKind.Correspondent, fields.CorrespondentId, cancellationToken).ConfigureAwait(false)
        && await VisibleAsync(ClassificationKind.DocumentType, fields.DocumentTypeId, cancellationToken).ConfigureAwait(false)
        && (fields.TagIds.Count == 0
            || await documents.AreVisibleAsync(current.Id, ClassificationKind.Tag, fields.TagIds, cancellationToken).ConfigureAwait(false));

    private Task<bool> VisibleAsync(ClassificationKind kind, Guid? entryId, CancellationToken cancellationToken) =>
        entryId is { } id ? documents.AreVisibleAsync(current.Id, kind, [id], cancellationToken) : Task.FromResult(true);
}
