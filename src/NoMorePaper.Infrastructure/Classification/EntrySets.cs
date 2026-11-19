using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Classification;
using NoMorePaper.Domain.Classification;
using NoMorePaper.Infrastructure.Persistence;

namespace NoMorePaper.Infrastructure.Classification;

/// <summary>The entries of one list as a query, all or only those an account may see (<see cref="VisibilitySql"/>).</summary>
internal static class EntrySets
{
    /// <summary>Tracked entries the audience may see (<see cref="VisibilitySql.Entries"/>); EF composes further filters around the rule.</summary>
    public static IQueryable<ClassificationEntry> Visible(AppDbContext db, ClassificationKind kind, Guid viewerId, EntryAudience audience)
    {
        var sql = VisibilitySql.Entries(kind, viewerId, audience);
        return kind switch
        {
            ClassificationKind.Correspondent => db.Correspondents.FromSql(sql),
            ClassificationKind.DocumentType => db.DocumentTypes.FromSql(sql),
            ClassificationKind.Tag => db.Tags.FromSql(sql),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown list."),
        };
    }

    /// <summary>All entries of the list, regardless of who may see them; only for ids already known to be visible.</summary>
    public static IQueryable<ClassificationEntry> All(AppDbContext db, ClassificationKind kind) => kind switch
    {
        ClassificationKind.Correspondent => db.Correspondents,
        ClassificationKind.DocumentType => db.DocumentTypes,
        ClassificationKind.Tag => db.Tags,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown list."),
    };

    /// <summary>Whether the audience may see every one of <paramref name="entryIds"/>.</summary>
    public static async Task<bool> AllVisibleAsync(
        AppDbContext db, ClassificationKind kind, Guid viewerId, IReadOnlyCollection<Guid> entryIds, EntryAudience audience, CancellationToken cancellationToken)
    {
        var ids = entryIds.Distinct().ToList();
        var visible = await Visible(db, kind, viewerId, audience).CountAsync(entry => ids.Contains(entry.Id), cancellationToken).ConfigureAwait(false);
        return visible == ids.Count;
    }
}
