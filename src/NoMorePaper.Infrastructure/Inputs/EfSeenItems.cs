using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Inputs;
using NoMorePaper.Infrastructure.Persistence;

namespace NoMorePaper.Infrastructure.Inputs;

/// <summary>
/// Seen items in Postgres (<c>input_seen_items</c>). Keys (Message-ID, file id) are stored only as their
/// SHA-256, so the table never names a mail or a file.
/// </summary>
public sealed class EfSeenItems(AppDbContext db) : ISeenItems
{
    public Task<bool> HasSeenAsync(InputKind kind, Guid? accountId, SeenList list, string key, CancellationToken cancellationToken)
    {
        var hash = Hash(key);
        var accountKey = accountId ?? Guid.Empty;
        return db.Set<SeenItemRow>().AsNoTracking()
            .AnyAsync(row => row.Kind == kind && row.AccountKey == accountKey && row.List == list && row.KeyHash == hash, cancellationToken);
    }

    public async Task MarkSeenAsync(InputKind kind, Guid? accountId, SeenList list, string key, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var (kindName, accountKey, hash) = (kind.ToString(), accountId ?? Guid.Empty, Hash(key));
        var listName = list.ToString();
        var utc = at.ToUniversalTime();
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO input_seen_items (kind, account_key, account_id, list, key_hash, failures, seen_at)
            VALUES ({kindName}, {accountKey}, {accountId}, {listName}, {hash}, 0, {utc})
            ON CONFLICT (kind, account_key, list, key_hash) DO NOTHING
            """,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> IncrementFailuresAsync(InputKind kind, Guid? accountId, string key, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var (kindName, accountKey, hash) = (kind.ToString(), accountId ?? Guid.Empty, Hash(key));
        var listName = nameof(SeenList.Failed);
        var utc = at.ToUniversalTime();
        var counts = await db.Database.SqlQuery<int>(
            $"""
            INSERT INTO input_seen_items (kind, account_key, account_id, list, key_hash, failures, seen_at)
            VALUES ({kindName}, {accountKey}, {accountId}, {listName}, {hash}, 1, {utc})
            ON CONFLICT (kind, account_key, list, key_hash) DO UPDATE SET
                failures = input_seen_items.failures + 1, seen_at = EXCLUDED.seen_at
            RETURNING failures AS "Value"
            """).ToListAsync(cancellationToken).ConfigureAwait(false);
        return counts.Single();
    }

    public async Task ForgetAsync(InputKind kind, Guid? accountId, SeenList list, string key, CancellationToken cancellationToken)
    {
        var hash = Hash(key);
        var accountKey = accountId ?? Guid.Empty;
        await db.Set<SeenItemRow>()
            .Where(row => row.Kind == kind && row.AccountKey == accountKey && row.List == list && row.KeyHash == hash)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static string Hash(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
    }
}
