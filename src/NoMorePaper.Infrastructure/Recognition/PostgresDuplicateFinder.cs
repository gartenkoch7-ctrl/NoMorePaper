using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Recognition;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Persistence;

namespace NoMorePaper.Infrastructure.Recognition;

/// <summary>
/// Content duplicates by trigram similarity (pg_trgm) of the OCR texts. The <c>%</c> operator uses the
/// trigram index on <c>content_text</c>; its threshold is set for this transaction only. Candidates are
/// filtered by the privacy rule in the query, so a hint never points at another account's private document.
/// </summary>
public sealed class PostgresDuplicateFinder(AppDbContext db) : IDuplicateFinder
{
    public async Task<Guid?> FindSimilarAsync(Guid documentId, double threshold, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(threshold, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(threshold, 1);
        var done = nameof(ProcessingState.Done);
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            // SET LOCAL as a function, because SET cannot take a parameter.
            var value = threshold.ToString(CultureInfo.InvariantCulture);
            await db.Database.ExecuteSqlAsync($"SELECT set_config('pg_trgm.similarity_threshold', {value}, true)", cancellationToken)
                .ConfigureAwait(false);

            // A document without owner (M1, before setup) is never private, so it counts as shared. A
            // document marked as duplicate of this one is not its original: the hint points one way only.
            // Neither side may lie in the trash. {0} is the document, {1} the state Done.
            var found = await db.Database.SqlQuery<Guid>(FormattableStringFactory.Create(
                    $$"""
                    SELECT o.id AS "Value" FROM documents d
                    JOIN documents o ON o.id <> d.id AND o.content_text % d.content_text
                    WHERE d.id = {0} AND {{TrashSql.NotInTrash}} AND {{TrashSql.NotInTrashOf("o")}}
                      AND o.state = {1} AND (o.owner_id = d.owner_id OR NOT o.is_private)
                      AND o.possible_duplicate_of IS DISTINCT FROM d.id
                    ORDER BY similarity(o.content_text, d.content_text) DESC, o.received_at, o.id
                    LIMIT 1
                    """,
                    documentId,
                    done))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return found.Count == 0 ? null : found[0];
        }
    }

    public async Task<bool> MarkAsync(Guid documentId, Guid duplicateOf, CancellationToken cancellationToken)
    {
        var document = await db.Documents.SingleOrDefaultAsync(found => found.Id == documentId, cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return false;
        }

        document.MarkPossibleDuplicate(duplicateOf);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}
