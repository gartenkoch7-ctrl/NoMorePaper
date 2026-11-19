using Microsoft.EntityFrameworkCore;
using NoMorePaper.Application.Inputs;
using NoMorePaper.Infrastructure.Persistence;

namespace NoMorePaper.Infrastructure.Inputs;

/// <summary>The state of the inputs in Postgres (<c>input_status</c>), one row per input and account.</summary>
public sealed class EfInputStatusStore(AppDbContext db) : IInputStatusStore
{
    public async Task RecordAsync(
        InputKind kind, Guid? accountId, int received, InputProblem? problem, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var kindName = kind.ToString();
        var key = accountId ?? Guid.Empty;
        var utc = at.ToUniversalTime();
        DateTimeOffset? success = problem is null ? utc : null;
        var problemName = problem?.Kind.ToString();
        var attention = problem?.NeedsAttention ?? false;
        long count = received;

        // One statement: two runs recording at once both count their documents.
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO input_status (kind, account_key, account_id, last_run_at, last_success_at, problem, needs_attention, received_total)
            VALUES ({kindName}, {key}, {accountId}, {utc}, {success}, {problemName}, {attention}, {count})
            ON CONFLICT (kind, account_key) DO UPDATE SET
                last_run_at = EXCLUDED.last_run_at,
                last_success_at = COALESCE(EXCLUDED.last_success_at, input_status.last_success_at),
                problem = EXCLUDED.problem,
                needs_attention = EXCLUDED.needs_attention,
                received_total = input_status.received_total + EXCLUDED.received_total
            """,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<InputStatus>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await db.Set<InputStatusRow>().AsNoTracking()
            .OrderBy(row => row.Kind)
            .ThenBy(row => row.AccountKey)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. rows.Select(row => new InputStatus(
            row.Kind, row.AccountId, row.LastRunAt, row.LastSuccessAt, row.Problem, row.NeedsAttention, row.ReceivedTotal))];
    }
}
