using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Classification;
using NoMorePaper.Domain.Classification;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Persistence;

namespace NoMorePaper.Infrastructure.Classification;

/// <summary>
/// Fields, editors and "to review" in Postgres. Every query filters by the viewer in SQL: documents by
/// the privacy rule (own or not private), review items by the editing rule, duplicate candidates by
/// the privacy rule again.
/// </summary>
public sealed class EfDocumentFieldsStore(AppDbContext db) : IDocumentFieldsStore
{
    private static readonly Expression<Func<Document, FieldsRow>> s_toRow = document => new FieldsRow(
        document.Id,
        document.Title,
        document.ReceivedAt,
        document.OwnerId,
        document.IsPrivate,
        document.CorrespondentId,
        document.DocumentTypeId,
        document.DocumentDate,
        document.Amount,
        document.Summary,
        document.Recognition,
        document.PossibleDuplicateOf,
        document.Tags.Select(tag => tag.TagId).ToList(),
        document.Editors.Select(editor => editor.AccountId).ToList());

    public Task<Document?> FindVisibleAsync(Guid documentId, Guid viewerId, CancellationToken cancellationToken) =>
        VisibleTo(viewerId).SingleOrDefaultAsync(document => document.Id == documentId, cancellationToken);

    public Task<bool> AreVisibleAsync(Guid viewerId, ClassificationKind kind, IReadOnlyCollection<Guid> entryIds, CancellationToken cancellationToken) =>
        EntrySets.AllVisibleAsync(db, kind, viewerId, entryIds, cancellationToken);

    public Task SaveAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    public async Task DeleteAsync(Document document, CancellationToken cancellationToken)
    {
        // Files, tags and editors are owned and go with the document; the database cascades the
        // processing job and clears duplicate hints that point here.
        db.Documents.Remove(document);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<DocumentFieldsView?> GetFieldsAsync(Guid documentId, Guid viewerId, CancellationToken cancellationToken)
    {
        var row = await VisibleTo(viewerId).AsNoTracking()
            .Where(document => document.Id == documentId)
            .Select(s_toRow)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            return null;
        }

        var lookup = await new FieldsReader(db).LoadAsync([row], viewerId, cancellationToken).ConfigureAwait(false);
        return lookup.View(row, viewerId);
    }

    public async Task<IReadOnlyList<ReviewItem>> ListReviewAsync(Guid viewerId, CancellationToken cancellationToken)
    {
        // The editing rule of Document.CanEdit; an editor of a private document is no editor.
        var rows = await db.Documents.AsNoTracking()
            .Where(document => document.OwnerId != null
                && (document.OwnerId == viewerId || (!document.IsPrivate && document.Editors.Any(editor => editor.AccountId == viewerId))))
            .Where(document => document.Recognition == RecognitionState.Automatic
                || db.Documents.Any(other => other.Id == document.PossibleDuplicateOf && (other.OwnerId == viewerId || !other.IsPrivate)))
            .OrderByDescending(document => document.ReceivedAt)
            .ThenByDescending(document => document.Id)
            .Select(s_toRow)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var lookup = await new FieldsReader(db).LoadAsync(rows, viewerId, cancellationToken).ConfigureAwait(false);
        return [.. rows.Select(row => new ReviewItem(row.Id, row.Title, row.ReceivedAt, lookup.View(row, viewerId), lookup.Candidate(row)))];
    }

    // The privacy rule of Document.IsVisibleTo, as SQL.
    private IQueryable<Document> VisibleTo(Guid viewerId) =>
        db.Documents.Where(document => document.OwnerId == viewerId || !document.IsPrivate);
}
