using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Persistence;

namespace NoMorePaper.Infrastructure.Tests.Trash;

/// <summary>Moves a document tracked by <paramref name="db"/> to the trash straight through the model.</summary>
internal static class Trashed
{
    public static readonly DateTimeOffset At = new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);

    public static async Task MoveAsync(AppDbContext db, Document document)
    {
        document.MoveToTrash(document.OwnerId!.Value, At);
        await db.SaveChangesAsync();
    }
}
