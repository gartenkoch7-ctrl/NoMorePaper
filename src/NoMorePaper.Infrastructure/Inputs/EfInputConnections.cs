using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Inputs;
using NoMorePaper.Infrastructure.Persistence;

namespace NoMorePaper.Infrastructure.Inputs;

/// <summary>Set-ups of the inputs in Postgres (<c>input_connections</c>); secrets arrive and leave encrypted.</summary>
public sealed class EfInputConnections(AppDbContext db) : IInputConnections
{
    public async Task<InputConnection?> GetAsync(InputKind kind, Guid? accountId, CancellationToken cancellationToken)
    {
        var key = accountId ?? Guid.Empty;
        var row = await db.Set<InputConnectionRow>().AsNoTracking()
            .SingleOrDefaultAsync(found => found.Kind == kind && found.AccountKey == key, cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : ToConnection(row);
    }

    public async Task SaveAsync(InputConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var kind = connection.Kind.ToString();
        var key = connection.AccountId ?? Guid.Empty;
        var updatedAt = connection.UpdatedAt.ToUniversalTime();

        // One statement, so saving twice at once leaves one row, the later one.
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO input_connections (kind, account_key, account_id, settings, secret, updated_at)
            VALUES ({kind}, {key}, {connection.AccountId}, CAST({connection.Settings} AS jsonb), {connection.Secret}, {updatedAt})
            ON CONFLICT (kind, account_key) DO UPDATE SET
                settings = EXCLUDED.settings, secret = EXCLUDED.secret, updated_at = EXCLUDED.updated_at
            """,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> DeleteAsync(InputKind kind, Guid? accountId, CancellationToken cancellationToken)
    {
        var key = accountId ?? Guid.Empty;
        return await db.Set<InputConnectionRow>()
            .Where(row => row.Kind == kind && row.AccountKey == key)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false) > 0;
    }

    public async Task<IReadOnlyList<InputConnection>> ListAsync(InputKind kind, CancellationToken cancellationToken)
    {
        var rows = await db.Set<InputConnectionRow>().AsNoTracking()
            .Where(row => row.Kind == kind)
            .OrderBy(row => row.AccountKey)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. rows.Select(ToConnection)];
    }

    private static InputConnection ToConnection(InputConnectionRow row) =>
        new(row.Kind, row.AccountId, row.Settings, row.Secret, row.UpdatedAt);
}
