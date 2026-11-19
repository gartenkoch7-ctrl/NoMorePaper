using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Classification;
using NoMorePaper.Domain.Classification;
using NoMorePaper.Infrastructure.Persistence;

namespace NoMorePaper.Infrastructure.Classification;

/// <summary>
/// The maintained lists in Postgres. Every read goes through the visibility rule (<see cref="VisibilitySql"/>);
/// renaming and merging also through the rights rule (<see cref="EntryRightsSql"/>), checked in the same
/// transaction as the change, with the entries locked.
/// </summary>
public sealed class ClassificationStore(AppDbContext db, TimeProvider clock) : IClassificationStore
{
    public async Task<IReadOnlyList<EntrySummary>> VisibleEntriesAsync(Guid viewerId, ClassificationKind kind, CancellationToken cancellationToken) =>
        await EntrySets.Visible(db, kind, viewerId)
            .AsNoTracking()
            .OrderBy(entry => entry.Name)
            .ThenBy(entry => entry.Id)
            .Select(entry => new EntrySummary(entry.Id, entry.Name))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<EntrySummary> FindOrCreateAsync(Guid actorId, ClassificationKind kind, string name, CancellationToken cancellationToken)
    {
        var cleaned = ClassificationName.Clean(name, nameof(name));
        var normalized = ClassificationName.Normalize(cleaned);

        // The oldest of several equal entries, so repeated calls agree. Entries the actor may not see are
        // never matched: that would attach (and reveal) another account's private entry.
        var found = await EntrySets.Visible(db, kind, actorId)
            .AsNoTracking()
            .Where(entry => entry.NormalizedName == normalized)
            .OrderBy(entry => entry.Id)
            .Select(entry => new EntrySummary(entry.Id, entry.Name))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (found is not null)
        {
            return found;
        }

        var now = clock.GetUtcNow();
        ClassificationEntry created = kind switch
        {
            ClassificationKind.Correspondent => Correspondent.Create(cleaned, actorId, now),
            ClassificationKind.DocumentType => DocumentType.Create(cleaned, actorId, now),
            ClassificationKind.Tag => Tag.Create(cleaned, actorId, now),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown list."),
        };
        db.Add(created);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new EntrySummary(created.Id, created.Name);
    }

    public async Task<EntryChange> RenameAsync(Guid actorId, ClassificationKind kind, Guid entryId, string name, CancellationToken cancellationToken)
    {
        var cleaned = ClassificationName.Clean(name, nameof(name));
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var change = await CheckAsync(actorId, kind, [entryId], cancellationToken).ConfigureAwait(false);
            if (change != EntryChange.Changed)
            {
                return change;
            }

            var entry = await EntrySets.All(db, kind).SingleAsync(found => found.Id == entryId, cancellationToken).ConfigureAwait(false);
            entry.Rename(cleaned);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return EntryChange.Changed;
        }
    }

    public async Task<EntryChange> MergeAsync(
        Guid actorId,
        ClassificationKind kind,
        Guid targetId,
        IReadOnlyCollection<Guid> sourceIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceIds);
        var sources = sourceIds.Where(id => id != targetId).Distinct().ToArray();
        if (sources.Length == 0)
        {
            throw new ArgumentException("Merging needs at least one entry besides the target.", nameof(sourceIds));
        }

        var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var change = await CheckAsync(actorId, kind, [targetId, .. sources], cancellationToken).ConfigureAwait(false);
            if (change != EntryChange.Changed)
            {
                return change;
            }

            await db.Database.ExecuteSqlAsync(EntryMergeSql.MoveDocuments(kind, targetId, sources), cancellationToken).ConfigureAwait(false);
            await db.Database.ExecuteSqlAsync(EntryMergeSql.DeleteEntries(kind, sources), cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return EntryChange.Changed;
        }
    }

    // Inside the caller's transaction: lock, then "not found" before "forbidden", so an entry the actor
    // may not see answers like one that does not exist.
    private async Task<EntryChange> CheckAsync(Guid actorId, ClassificationKind kind, Guid[] entryIds, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlAsync(EntryRightsSql.Lock(kind, entryIds), cancellationToken).ConfigureAwait(false);
        if (!await EntrySets.AllVisibleAsync(db, kind, actorId, entryIds, cancellationToken).ConfigureAwait(false))
        {
            return EntryChange.NotFound;
        }

        var forbidden = await db.Database.SqlQuery<bool>(EntryRightsSql.AnyDocumentNotEditable(kind, actorId, entryIds))
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);
        return forbidden ? EntryChange.Forbidden : EntryChange.Changed;
    }
}
